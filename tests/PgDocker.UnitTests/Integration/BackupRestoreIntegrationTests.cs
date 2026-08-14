using Microsoft.Extensions.DependencyInjection;
using PgDocker.Core.Interfaces;
using PgDocker.Core.Models;
using PgDocker.Services;
using Serilog;

namespace PgDocker.UnitTests.Integration;

[Trait("Category", "Integration")]
public class BackupRestoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture = new();
    private string _tempBackupPath = null!;
    private string _configPath = null!;
    private IServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();

        _tempBackupPath = Path.Combine(Path.GetTempPath(), $"pgdocker-test-{Guid.NewGuid().ToString()[..8]}");
        Directory.CreateDirectory(_tempBackupPath);

        _configPath = Path.Combine(_tempBackupPath, "pgdocker.yml");
        var yaml = $@"postgres:
  container: {_fixture.ContainerName}
  username: postgres
backup:
  path: {_tempBackupPath}
  compression: true
  verify: true
  excludeDatabases:
    - template0
    - template1
retention:
  localDays: 30
  remoteDays: 90
";
        File.WriteAllText(_configPath, yaml);

        var services = new ServiceCollection();
        Log.Logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        services.AddSingleton(Log.Logger);
        services.AddScoped<IDockerService, DockerService>();
        services.AddScoped<IPostgresService, PostgresService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<IHashService, HashService>();
        services.AddScoped<IManifestService, ManifestService>();
        services.AddScoped<ICompressionService, CompressionService>();
        services.AddScoped<IBackupLocatorService, BackupLocatorService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IVerifyService, VerifyService>();
        services.AddScoped<IRestoreService, RestoreService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();

        if (Directory.Exists(_tempBackupPath))
            Directory.Delete(_tempBackupPath, recursive: true);
    }

    [Fact]
    public async Task Backup_CreatesManifestAndChecksums()
    {
        var backupService = _serviceProvider.GetRequiredService<IBackupService>();

        var backupPath = await backupService.ExecuteBackupAsync(_configPath, upload: false, prune: false);

        Assert.NotNull(backupPath);
        Assert.True(Directory.Exists(backupPath));
        Assert.True(File.Exists(Path.Combine(backupPath, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(backupPath, "sha256.txt")));
    }

    [Fact]
    public async Task Verify_ValidBackup_ReturnsTrue()
    {
        var backupService = _serviceProvider.GetRequiredService<IBackupService>();
        var verifyService = _serviceProvider.GetRequiredService<IVerifyService>();
        var locatorService = _serviceProvider.GetRequiredService<IBackupLocatorService>();

        await backupService.ExecuteBackupAsync(_configPath, upload: false, prune: false);

        var backups = await locatorService.ListBackupsAsync(_tempBackupPath);
        Assert.NotEmpty(backups);

        var backupName = backups[0].Name;
        var isValid = await verifyService.VerifyBackupAsync(_configPath, backupName);

        Assert.True(isValid);
    }

    [Fact]
    public async Task Verify_TamperedBackup_ReturnsFalse()
    {
        var backupService = _serviceProvider.GetRequiredService<IBackupService>();
        var verifyService = _serviceProvider.GetRequiredService<IVerifyService>();
        var locatorService = _serviceProvider.GetRequiredService<IBackupLocatorService>();

        await backupService.ExecuteBackupAsync(_configPath, upload: false, prune: false);

        var backups = await locatorService.ListBackupsAsync(_tempBackupPath);
        var backupName = backups[0].Name;
        var backupDir = await locatorService.ResolveBackupDirectoryAsync(_tempBackupPath, backupName);

        var dumpFiles = Directory.GetFiles(backupDir, "*.dump");
        if (dumpFiles.Length > 0)
        {
            var dumpFile = dumpFiles[0];
            var content = await File.ReadAllBytesAsync(dumpFile);
            if (content.Length > 0)
            {
                content[0] ^= 0xFF;
                await File.WriteAllBytesAsync(dumpFile, content);
            }
        }

        var isValid = await verifyService.VerifyBackupAsync(_configPath, backupName);

        Assert.False(isValid);
    }
}
