namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using Serilog;

public class VerifyService : IVerifyService
{
    private readonly IBackupLocatorService _backupLocator;
    private readonly IHashService _hashService;
    private readonly IConfigurationService _configService;
    private readonly ILogger _logger;

    public VerifyService(
        IBackupLocatorService backupLocator,
        IHashService hashService,
        IConfigurationService configService,
        ILogger logger)
    {
        _backupLocator = backupLocator;
        _hashService = hashService;
        _configService = configService;
        _logger = logger;
    }

    public async Task<bool> VerifyBackupAsync(string configPath, string backupName)
    {
        try
        {
            _logger.Information("Verifying backup: {BackupName}", backupName);

            var config = await _configService.LoadConfigurationAsync(configPath);
            var backupPath = config.Backup.Path;

            // Resolve backup directory
            var backupDir = await _backupLocator.ResolveBackupDirectoryAsync(backupPath, backupName);

            // Read stored hashes
            var storedHashes = await _hashService.ReadSha256FileAsync(backupDir);

            if (storedHashes.Count == 0)
            {
                _logger.Warning("No hashes found in backup");
                return false;
            }

            // Verify each file
            bool allValid = true;
            foreach (var (filePath, storedHash) in storedHashes)
            {
                var fileName = Path.GetFileName(filePath);
                var fullPath = Path.Combine(backupDir, fileName);

                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"✗ {fileName} (missing)");
                    allValid = false;
                    continue;
                }

                var calculatedHash = await _hashService.CalculateFileHashAsync(fullPath);
                if (calculatedHash == storedHash)
                {
                    Console.WriteLine($"✓ {fileName}");
                }
                else
                {
                    Console.WriteLine($"✗ {fileName} (hash mismatch)");
                    _logger.Warning("Hash mismatch for {File}: expected {Expected}, got {Calculated}",
                        fileName, storedHash, calculatedHash);
                    allValid = false;
                }
            }

            if (allValid)
            {
                Console.WriteLine();
                Console.WriteLine("Backup integrity: OK");
                _logger.Information("Backup verification passed");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Backup integrity: FAILED");
                _logger.Warning("Backup verification failed");
            }

            return allValid;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Backup verification failed");
            throw;
        }
    }
}
