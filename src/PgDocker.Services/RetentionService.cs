namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using Serilog;

public class RetentionService : IRetentionService
{
    private readonly IBackupLocatorService _backupLocator;
    private readonly IConfigurationService _configService;
    private readonly ILogger _logger;

    public RetentionService(
        IBackupLocatorService backupLocator,
        IConfigurationService configService,
        ILogger logger)
    {
        _backupLocator = backupLocator;
        _configService = configService;
        _logger = logger;
    }

    public async Task PruneBackupsAsync(string configPath, bool dryRun = false)
    {
        try
        {
            _logger.Information("Starting retention policy cleanup (dry-run: {DryRun})", dryRun);

            var config = await _configService.LoadConfigurationAsync(configPath);
            var backupPath = config.Backup.Path;
            var localDays = config.Retention.LocalDays;

            if (!Directory.Exists(backupPath))
            {
                _logger.Warning("Backup path does not exist: {BackupPath}", backupPath);
                return;
            }

            var backups = await _backupLocator.ListBackupsAsync(backupPath);
            var cutoffDate = DateTime.UtcNow.AddDays(-localDays);

            _logger.Information("Retention policy: keep backups from last {Days} days (cutoff: {CutoffDate})",
                localDays, cutoffDate);

            var backupsToDelete = backups.Where(b => b.CreatedAt < cutoffDate).ToList();

            if (backupsToDelete.Count == 0)
            {
                Console.WriteLine("No backups to delete");
                _logger.Information("No backups exceed retention policy");
                return;
            }

            foreach (var backup in backupsToDelete)
            {
                var size = FormatBytes(backup.SizeBytes);
                Console.WriteLine($"  {backup.Name} ({backup.CreatedAt:yyyy-MM-dd HH:mm:ss}) - {size}");

                if (!dryRun)
                {
                    // Delete directory if it exists
                    var backupDir = Path.Combine(backupPath, backup.Name);
                    if (Directory.Exists(backupDir))
                    {
                        Directory.Delete(backupDir, recursive: true);
                        _logger.Information("Deleted backup directory: {BackupDir}", backupDir);
                    }

                    // Delete compressed files if they exist
                    var zipPath = Path.Combine(backupPath, $"{backup.Name}.zip");
                    var tarGzPath = Path.Combine(backupPath, $"{backup.Name}.tar.gz");

                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                        _logger.Information("Deleted backup archive: {ArchivePath}", zipPath);
                    }

                    if (File.Exists(tarGzPath))
                    {
                        File.Delete(tarGzPath);
                        _logger.Information("Deleted backup archive: {ArchivePath}", tarGzPath);
                    }
                }
            }

            if (dryRun)
            {
                Console.WriteLine($"\nDry-run: {backupsToDelete.Count} backup(s) would be deleted");
                _logger.Information("Dry-run: {Count} backup(s) would be deleted", backupsToDelete.Count);
            }
            else
            {
                Console.WriteLine($"\n✓ Deleted {backupsToDelete.Count} backup(s)");
                _logger.Information("Retention cleanup completed. Deleted {Count} backup(s)", backupsToDelete.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Retention policy cleanup failed");
            throw;
        }
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##}{sizes[order]}";
    }
}
