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
    /// 备份与数据同盘只防误删/损坏，不防磁盘故障。注意 BACKUP DATABASE 以 SqlServer
    /// 服务账号写文件：该目录须同时授予应用进程与 SqlServer 服务账号读写权限。
    /// </summary>
    public string Directory { get; set; } = "backup";

    /// <summary>备份文件保留天数（过期 .bak 每次备份时清理）</summary>
    public int RetentionDays { get; set; } = 7;
}

/// <summary>备份结果</summary>
/// <param name="BakFile">数据库备份文件路径</param>
/// <param name="BakSizeBytes">备份文件大小（字节）</param>
/// <param name="MirroredFiles">本次镜像复制的文件数（未变化的不计）</param>
public sealed record BackupResult(string BakFile, long BakSizeBytes, int MirroredFiles);

/// <summary>数据备份：SqlServer 全量备份 + 上传文件增量镜像 + 保留期清理</summary>
public interface IBackupService
{
    /// <summary>
    /// 执行备份。注意 BACKUP DATABASE 的目标路径是数据库服务器本机路径——
    /// 单机部署（应用与 SqlServer 同机）天然成立；远程库需配置共享目录。数据库账号需具备备份权限。
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

        // ① SqlServer 全量备份（当前连接库，文件名带时间戳，WITH INIT 覆盖同名残留）
        var dbName = db.Ado.GetString("SELECT DB_NAME()");
        var bakFile = Path.Combine(baseDir, $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
        await db.Ado.ExecuteCommandAsync(
            $"BACKUP DATABASE [{dbName.Replace("]", "]]")}] TO DISK = @path WITH INIT",
            new SugarParameter("@path", bakFile));

        // ② 保留期：删除过期 .bak（本次刚生成的跳过）
        var cutoff = DateTime.Now.AddDays(-opt.RetentionDays);
        foreach (var expired in Directory.GetFiles(baseDir, "*.bak"))
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
