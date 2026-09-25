using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetBase.Api.Jobs;
using NetBase.Service.Sys;
using SqlSugar;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 备份服务集成测试：对真实测试库执行 pg_dump（custom 格式），验证 .backup 生成、
/// 上传文件增量镜像与保留期清理。pg_dump 不可用的环境（本机无 client 工具）验证优雅失败——
/// 完整备份断言在 CI（装 postgresql-client-17）执行。
/// </summary>
[Collection("Integration")]
public class BackupServiceTests
{
    private readonly IntegrationFixture _fixture;

    public BackupServiceTests(IntegrationFixture fixture) => _fixture = fixture;

    private BackupService CreateService(string backupDir, string storageRoot) =>
        new(_fixture.GetService<ISqlSugarClient>(),
            Options.Create(new BackupOptions { Directory = backupDir, RetentionDays = 7 }),
            Options.Create(new FileStorageOptions { RootPath = storageRoot }),
            environment: null,
            NullLogger<BackupService>.Instance);

    /// <summary>pg_dump 可用性（版本探测；CI 装 postgresql-client-17，本机可经 NETBASE_PGDUMP 指定路径）</summary>
    private static bool PgDumpAvailable()
    {
        var path = Environment.GetEnvironmentVariable("NETBASE_PGDUMP") ?? "pg_dump";
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(path, "--version")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string CreateBackupDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"nb_bak_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task Backup_ShouldCreateBakFileAndMirrorFiles()
    {
        var dir = CreateBackupDir();
        var storage = Path.Combine(dir, "_src_uploads");
        Directory.CreateDirectory(Path.Combine(storage, "20260910"));
        await File.WriteAllTextAsync(Path.Combine(storage, "20260910", "a.txt"), "hello");

        if (!PgDumpAvailable())
        {
            // 无 pg_dump：备份须优雅失败（作业层据此发站内信通知管理员），不静默假成功
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService(dir, storage).RunAsync());
            Assert.Contains("pg_dump", ex.Message, StringComparison.OrdinalIgnoreCase);
            return;
        }

        var result = await CreateService(dir, storage).RunAsync();

        // 数据库备份：真实 .backup 文件生成
        Assert.True(File.Exists(result.BakFile));
        Assert.True(result.BakSizeBytes > 0);
        // 文件镜像：源目录结构复制到 备份目录/files
        Assert.True(File.Exists(Path.Combine(dir, "files", "20260910", "a.txt")));

        // 二次备份：新 .backup 文件（文件名秒级时间戳，跨秒执行）+ 增量镜像新文件
        await File.WriteAllTextAsync(Path.Combine(storage, "20260910", "b.txt"), "new");
        await Task.Delay(1100);
        var second = await CreateService(dir, storage).RunAsync();
        Assert.NotEqual(result.BakFile, second.BakFile);
        Assert.True(File.Exists(Path.Combine(dir, "files", "20260910", "b.txt")));

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task Backup_Retention_ShouldDeleteExpiredBakFiles()
    {
        if (!PgDumpAvailable())
        {
            return; // 保留期清理依附于备份成功路径，本机无 pg_dump 时由 CI 覆盖
        }

        var dir = CreateBackupDir();
        var expired = Path.Combine(dir, "netbase_test_20200101_000000.backup");
        await File.WriteAllTextAsync(expired, "old");
        File.SetLastWriteTime(expired, DateTime.Now.AddDays(-10)); // 超过保留期 7 天

        var result = await CreateService(dir, Path.Combine(dir, "_empty_uploads")).RunAsync();

        Assert.False(File.Exists(expired)); // 过期备份被清理
        Assert.True(File.Exists(result.BakFile)); // 本次生成的保留
        Directory.Delete(dir, recursive: true);
    }
}
