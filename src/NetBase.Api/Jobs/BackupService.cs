using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetBase.Service.Sys;
using SqlSugar;

namespace NetBase.Api.Jobs;

/// <summary>备份配置（appsettings.json 的 Backup 节点）</summary>
public class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>是否启用内置备份作业（关闭时启动移除既有调度）</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 备份目录（相对 ContentRoot 或绝对路径）。生产建议指向另一块磁盘/挂载的网络共享——
    /// 备份与数据同盘只防误删/损坏，不防磁盘故障。
    /// </summary>
    public string Directory { get; set; } = "backup";

    /// <summary>备份文件保留天数（过期 .backup 每次备份时清理）</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>pg_dump 可执行文件路径（Docker 镜像内已装 postgresql-client-17）</summary>
    public string PgDumpPath { get; set; } = "pg_dump";
}

/// <summary>备份结果</summary>
/// <param name="BakFile">数据库备份文件路径</param>
/// <param name="BakSizeBytes">备份文件大小（字节）</param>
/// <param name="MirroredFiles">本次镜像复制的文件数（未变化的不计）</param>
public sealed record BackupResult(string BakFile, long BakSizeBytes, int MirroredFiles);

/// <summary>数据备份：PostgreSQL 全量备份（pg_dump custom 格式）+ 上传文件增量镜像 + 保留期清理</summary>
public interface IBackupService
{
    /// <summary>
    /// 执行备份。pg_dump 在应用进程所在机器/容器执行（网络拉取到备份目录），
    /// 需 PgDumpPath 可执行（Docker 镜像内置）；不可用时抛异常，由作业层通知管理员。
    /// </summary>
    Task<BackupResult> RunAsync(CancellationToken ct = default);
}

/// <summary>备份实现：每日作业调用，失败由作业层通知管理员</summary>
public class BackupService(
    ISqlSugarClient db,
    IOptions<BackupOptions> backupOptions,
    IOptions<FileStorageOptions> fileStorageOptions,
    IHostEnvironment? environment,
    ILogger<BackupService> logger) : IBackupService
{
    public async Task<BackupResult> RunAsync(CancellationToken ct = default)
    {
        var opt = backupOptions.Value;
        var baseDir = Path.IsPathRooted(opt.Directory)
            ? opt.Directory
            : Path.Combine(environment?.ContentRootPath ?? AppContext.BaseDirectory, opt.Directory);
        Directory.CreateDirectory(baseDir);

        // ① pg_dump 全量备份（custom 格式，压缩且可 pg_restore 恢复；文件名带时间戳）
        var bakFile = await DumpDatabaseAsync(baseDir, opt, ct);

        // ② 保留期：删除过期 .backup（本次刚生成的跳过）
        var cutoff = DateTime.Now.AddDays(-opt.RetentionDays);
        foreach (var expired in Directory.GetFiles(baseDir, "*.backup"))
        {
            if (!string.Equals(expired, bakFile, StringComparison.OrdinalIgnoreCase)
                && File.GetLastWriteTime(expired) < cutoff)
            {
                File.Delete(expired);
            }
        }

        // ③ 上传文件增量镜像：复制新增/变更文件到 备份目录/files（相同大小且不早于源文件的跳过）
        var storageRoot = string.IsNullOrWhiteSpace(fileStorageOptions.Value.RootPath)
            ? Path.Combine(environment?.ContentRootPath ?? AppContext.BaseDirectory, "uploads")
            : fileStorageOptions.Value.RootPath;
        var mirrored = Directory.Exists(storageRoot)
            ? MirrorDirectory(storageRoot, Path.Combine(baseDir, "files"))
            : 0;

        var size = new FileInfo(bakFile).Length;
        logger.LogInformation("备份完成: {File}（{Size:F1}MB）, 文件镜像 {Count} 个",
            bakFile, size / 1024.0 / 1024, mirrored);
        return new BackupResult(bakFile, size, mirrored);
    }

    /// <summary>调用 pg_dump 导出当前连接库（连接参数从连接串解析，密码经 PGPASSWORD 传递）</summary>
    private async Task<string> DumpDatabaseAsync(string baseDir, BackupOptions opt, CancellationToken ct)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(db.Ado.Connection.ConnectionString);
        var dbName = builder.Database ?? throw new InvalidOperationException("连接串缺少 Database，无法确定备份目标");
        var bakFile = Path.Combine(baseDir, $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.backup");

        var psi = new ProcessStartInfo
        {
            FileName = opt.PgDumpPath,
            // --no-password：凭据不可用时立即失败而非挂起等待输入；custom 格式经 pg_restore 恢复
            ArgumentList =
            {
                "--host", builder.Host ?? "localhost",
                "--port", builder.Port.ToString(),
                "--username", builder.Username ?? "",
                "--dbname", dbName,
                "--format=custom",
                "--no-password",
                "--file", bakFile
            },
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (!string.IsNullOrEmpty(builder.Password))
        {
            psi.Environment["PGPASSWORD"] = builder.Password;
        }

        Process process;
        try
        {
            process = Process.Start(psi)
                ?? throw new InvalidOperationException($"无法启动 {opt.PgDumpPath}，请检查 Backup:PgDumpPath 配置");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"无法启动 {opt.PgDumpPath}（{ex.Message}），请确认已安装 PostgreSQL 客户端或修正 Backup:PgDumpPath 配置", ex);
        }
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"pg_dump 退出码 {process.ExitCode}：{stderr.Trim()}");
        }
        return bakFile;
    }

    /// <summary>递归镜像：仅复制新增/变更文件，返回本次复制数</summary>
    private static int MirrorDirectory(string source, string target)
    {
        var copied = 0;
        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
        {
            var dest = Path.Combine(target, Path.GetFileName(file));
            if (File.Exists(dest)
                && new FileInfo(dest).Length == new FileInfo(file).Length
                && File.GetLastWriteTime(dest) >= File.GetLastWriteTime(file))
            {
                continue;
            }
            File.Copy(file, dest, overwrite: true);
            copied++;
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            copied += MirrorDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
        return copied;
    }
}
