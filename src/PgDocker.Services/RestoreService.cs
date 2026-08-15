namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using PgDocker.Core.Models;
using Serilog;

public class RestoreService : IRestoreService
{
    private readonly IBackupLocatorService _backupLocator;
    private readonly IVerifyService _verifyService;
    private readonly IManifestService _manifestService;
    private readonly IDockerService _dockerService;
    private readonly IConfigurationService _configService;
    private readonly ILogger _logger;

    public RestoreService(
        IBackupLocatorService backupLocator,
        IVerifyService verifyService,
        IManifestService manifestService,
        IDockerService dockerService,
        IConfigurationService configService,
        ILogger logger)
    {
        _backupLocator = backupLocator;
        _verifyService = verifyService;
        _manifestService = manifestService;
        _dockerService = dockerService;
        _configService = configService;
        _logger = logger;
    }

    public async Task ExecuteRestoreAsync(string configPath, string backupName, string? databaseName = null, bool clean = false, bool yes = false)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.Information("Starting restore for backup: {BackupName}", backupName);

            var config = await _configService.LoadConfigurationAsync(configPath);
            var containerName = config.Postgres.Container;
            var username = config.Postgres.Username;
            var backupPath = config.Backup.Path;

            // Verify backup integrity
            _logger.Information("Verifying backup integrity");
            var isValid = await _verifyService.VerifyBackupAsync(configPath, backupName);
            if (!isValid)
            {
                _logger.Error("Backup verification failed");
                throw new InvalidOperationException("Backup verification failed");
            }

            // Resolve backup directory
            var backupDir = await _backupLocator.ResolveBackupDirectoryAsync(backupPath, backupName);

            // Read manifest
            var manifest = await _manifestService.ReadManifestAsync(backupDir);

            // Restore globals.sql if restoring all databases
            if (string.IsNullOrEmpty(databaseName))
            {
                _logger.Information("Restoring global objects");
                var globalsPath = Path.Combine(backupDir, "globals.sql");
                if (File.Exists(globalsPath))
                {
                    using (var fileStream = File.OpenRead(globalsPath))
                    {
                        await _dockerService.ExecuteCommandWithInputAsync(
                            containerName,
                            "psql",
                            new[] { "-U", username, "-d", "postgres" },
                            fileStream
                        );
                    }
                }
            }

            // Restore databases
            var databasesToRestore = string.IsNullOrEmpty(databaseName)
                ? manifest.Databases.Select(d => d.Name).ToList()
                : new List<string> { databaseName };

            foreach (var db in databasesToRestore)
            {
                _logger.Information("Restoring database {Database}", db);

                // Check if database exists
                var dbExists = await DatabaseExistsAsync(containerName, username, db);

                if (dbExists && !clean)
                {
                    _logger.Error("Database {Database} already exists and --clean flag was not used", db);
                    throw new InvalidOperationException($"Database '{db}' already exists. Use --clean to drop and recreate it.");
                }

                // For dropping, use template1 if dropping postgres, otherwise postgres
                var adminDb = db.Equals("postgres", StringComparison.OrdinalIgnoreCase) ? "template1" : "postgres";

                if (dbExists && clean)
                {
                    _logger.Information("Dropping existing database {Database}", db);
                    await _dockerService.ExecuteCommandAndGetOutputAsync(
                        containerName,
                        "psql",
                        new[] { "-U", username, "-d", adminDb, "-c", $"DROP DATABASE IF EXISTS \"{db}\"" }
                    );
                }

                // Create database if it doesn't exist (or was just dropped)
                if (!dbExists || clean)
                {
                    _logger.Information("Creating database {Database}", db);
                    await _dockerService.ExecuteCommandAndGetOutputAsync(
                        containerName,
                        "psql",
                        new[] { "-U", username, "-d", adminDb, "-c", $"CREATE DATABASE \"{db}\"" }
                    );
                }

                // Restore database from dump
                var dumpFile = manifest.Databases.FirstOrDefault(d => d.Name == db)?.File;
                if (dumpFile != null)
                {
                    var dumpPath = Path.Combine(backupDir, dumpFile);
                    if (File.Exists(dumpPath))
                    {
                        _logger.Information("Restoring dump for database {Database}", db);
                        using (var fileStream = File.OpenRead(dumpPath))
                        {
                            await _dockerService.ExecuteCommandWithInputAsync(
                                containerName,
                                "pg_restore",
                                new[] { "-U", username, "-d", db, "-Fc" },
                                fileStream
                            );
                        }
                    }
                }
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.Information("Restore completed successfully in {Duration}ms", duration.TotalMilliseconds);
            Console.WriteLine($"✓ Restore completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Restore failed");
            throw;
        }
    }

    private async Task<bool> DatabaseExistsAsync(string containerName, string username, string databaseName)
    {
        try
        {
            var output = await _dockerService.ExecuteCommandAndGetOutputAsync(
                containerName,
                "psql",
                new[] { "-U", username, "-d", "postgres", "-tAc", $"SELECT 1 FROM pg_database WHERE datname='{databaseName}'" }
            );
            return !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }
}
