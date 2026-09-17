using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetBase.Api.Jobs;
using NetBase.Service.Sys;
using SqlSugar;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 备份服务集成测试：对真实测试库执行 BACKUP DATABASE，验证 .bak 生成、
/// 上传文件增量镜像与保留期清理。
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

    /// <summary>
    /// BACKUP DATABASE 以 SqlServer 服务账号写文件，默认无当前用户 Temp 目录权限——
    /// 给测试临时目录授权 Everyone（*S-1-1-0 为 SID 形式，规避系统本地化差异）。
    /// </summary>
    private static string CreateBackupDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"nb_bak_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var psi = new System.Diagnostics.ProcessStartInfo("icacls", $"\"{dir}\" /grant *S-1-1-0:(OI)(CI)F")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        System.Diagnostics.Process.Start(psi)?.WaitForExit(5000);
        return dir;
    }

    [Fact]
    public async Task Backup_ShouldCreateBakFileAndMirrorFiles()
    {
        var dir = CreateBackupDir();
        var storage = Path.Combine(dir, "_src_uploads");
        Directory.CreateDirectory(Path.Combine(storage, "20260910"));
        await File.WriteAllTextAsync(Path.Combine(storage, "20260910", "a.txt"), "hello");

        var result = await CreateService(dir, storage).RunAsync();

        // 数据库备份：真实 .bak 文件生成
        Assert.True(File.Exists(result.BakFile));
        Assert.True(result.BakSizeBytes > 0);
        // 文件镜像：源目录结构复制到 备份目录/files
        Assert.True(File.Exists(Path.Combine(dir, "files", "20260910", "a.txt")));

        // 二次备份：新 .bak 文件（文件名秒级时间戳，跨秒执行）+ 增量镜像新文件
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
        var dir = CreateBackupDir();
        var expired = Path.Combine(dir, "NetBase_Test_20200101_000000.bak");
        await File.WriteAllTextAsync(expired, "old");
        File.SetLastWriteTime(expired, DateTime.Now.AddDays(-10)); // 超过保留期 7 天

        var result = await CreateService(dir, Path.Combine(dir, "_empty_uploads")).RunAsync();

        Assert.False(File.Exists(expired)); // 过期备份被清理
        Assert.True(File.Exists(result.BakFile)); // 本次生成的保留
        Directory.Delete(dir, recursive: true);
    }
}
