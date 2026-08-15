namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using PgDocker.Core.Models;
using Serilog;

public class BackupService : IBackupService
{
    private readonly IDockerService _dockerService;
    private readonly IPostgresService _postgresService;
    private readonly IConfigurationService _configService;
    private readonly IHashService _hashService;
    private readonly IManifestService _manifestService;
    private readonly ICompressionService _compressionService;
    private readonly ILogger _logger;

    public BackupService(
        IDockerService dockerService,
        IPostgresService postgresService,
        IConfigurationService configService,
        IHashService hashService,
        IManifestService manifestService,
        ICompressionService compressionService,
        ILogger logger)
    {
        _dockerService = dockerService;
        _postgresService = postgresService;
        _configService = configService;
        _hashService = hashService;
        _manifestService = manifestService;
        _compressionService = compressionService;
        _logger = logger;
    }

    public async Task<string> ExecuteBackupAsync(string configPath, bool upload = false, bool prune = false, string? databaseName = null)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.Information("Starting backup");

            var config = await _configService.LoadConfigurationAsync(configPath);
            var containerName = config.Postgres.Container;
            var username = config.Postgres.Username;
            var backupPath = config.Backup.Path;

            // Ensure backup directory exists
            Directory.CreateDirectory(backupPath);

            // Check Docker availability
            if (!await _dockerService.IsDockerAvailableAsync())
            {
                _logger.Error("Docker is not available");
                throw new InvalidOperationException("Docker is not available");
            }

            _logger.Information("Docker is available");

            // Check container exists and is running
            if (!await _dockerService.ContainerExistsAsync(containerName))
            {
                _logger.Error("Container {Container} does not exist", containerName);
                throw new InvalidOperationException($"Container {containerName} does not exist");
            }

            if (!await _dockerService.ContainerIsRunningAsync(containerName))
            {
                _logger.Error("Container {Container} is not running", containerName);
                throw new InvalidOperationException($"Container {containerName} is not running");
            }

            _logger.Information("Container {Container} is running", containerName);

            // Get PostgreSQL version
            var pgVersion = await _postgresService.GetPostgresVersionAsync(containerName, username);
            _logger.Information("PostgreSQL {Version} detected", pgVersion);

            // Get database list
            var databases = await _postgresService.GetDatabaseListAsync(
                containerName,
                username,
                config.Backup.ExcludeDatabases
            );

            // Filter to specific database if provided
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (!databases.Contains(databaseName))
                    throw new InvalidOperationException($"Database '{databaseName}' not found");
                databases = new List<string> { databaseName };
            }

            _logger.Information("Found {Count} databases", databases.Count);

            // Create backup directory
            var backupDir = Path.Combine(
                backupPath,
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
            );
            Directory.CreateDirectory(backupDir);

            // Backup globals
            _logger.Information("Backing up global objects");
            var globalsPath = Path.Combine(backupDir, "globals.sql");
            using (var globalsStream = await _dockerService.ExecuteCommandAsync(
                containerName,
                "pg_dumpall",
                new[] { "-U", username, "--globals-only" }
            ))
            {
                using (var fileStream = File.Create(globalsPath))
                {
                    await globalsStream.CopyToAsync(fileStream);
                }
            }

            // Backup each database
            var fileHashes = new Dictionary<string, string>();
            foreach (var database in databases)
            {
                _logger.Information("Backing up database {Database}", database);
                var dumpPath = Path.Combine(backupDir, $"{database}.dump");

                using (var dumpStream = await _dockerService.ExecuteCommandAsync(
                    containerName,
                    "pg_dump",
                    new[] { "-U", username, "-Fc", database }
                ))
                {
                    using (var fileStream = File.Create(dumpPath))
                    {
                        await dumpStream.CopyToAsync(fileStream);
                    }
                }
            }

            // Calculate hashes
            _logger.Information("Calculating hashes");
            var globalsHash = await _hashService.CalculateFileHashAsync(globalsPath);
            fileHashes[globalsPath] = globalsHash;

            foreach (var database in databases)
            {
                var dumpPath = Path.Combine(backupDir, $"{database}.dump");
                var hash = await _hashService.CalculateFileHashAsync(dumpPath);
                fileHashes[dumpPath] = hash;
            }

            // Write manifest
            _logger.Information("Creating manifest");
            var manifest = new BackupManifest
            {
                ToolVersion = "1.0.0",
                CreatedAt = DateTime.UtcNow,
                Container = containerName,
                PostgresVersion = pgVersion,
                Databases = databases.Select(db => new DatabaseEntry
                {
                    Name = db,
                    File = $"{db}.dump"
                }).ToList()
            };

            await _manifestService.WriteManifestAsync(backupDir, manifest);

            // Write SHA256 file
            _logger.Information("Writing SHA256 checksums");
            await _hashService.WriteSha256FileAsync(backupDir, fileHashes);

            // Compress
            if (config.Backup.Compression)
            {
                _logger.Information("Compressing backup");
                await _compressionService.CompressDirectoryAsync(backupDir);
                _logger.Information("Compression completed");
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.Information("Backup completed successfully in {Duration}ms", duration.TotalMilliseconds);

            return backupDir;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Backup failed");
            throw;
        }
    }
}
