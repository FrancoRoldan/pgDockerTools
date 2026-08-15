using Microsoft.Extensions.DependencyInjection;
using PgDocker.Core.Interfaces;
using PgDocker.Services;
using Serilog;
using Serilog.Events;

var services = new ServiceCollection();

var minLevel = LogEventLevel.Information;
if (args.Contains("-v") || args.Contains("--verbose"))
    minLevel = LogEventLevel.Debug;
else if (args.Contains("-q") || args.Contains("--quiet"))
    minLevel = LogEventLevel.Warning;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(minLevel)
    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
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
services.AddScoped<ISftpService, SftpService>();
services.AddScoped<IRetentionService, RetentionService>();

var serviceProvider = services.BuildServiceProvider();

try
{
    if (args.Length == 0)
    {
        ShowHelp();
        return (int)PgDocker.Core.ExitCode.Success;
    }

    var command = args[0];

    switch (command)
    {
        case "backup":
            await HandleBackup(args, serviceProvider.GetRequiredService<IBackupService>());
            return (int)PgDocker.Core.ExitCode.Success;

        case "restore":
            await HandleRestore(args, serviceProvider.GetRequiredService<IRestoreService>());
            return (int)PgDocker.Core.ExitCode.Success;

        case "verify":
            await HandleVerify(args, serviceProvider.GetRequiredService<IVerifyService>());
            return (int)PgDocker.Core.ExitCode.Success;

        case "list":
            await HandleList(args, serviceProvider.GetRequiredService<IBackupLocatorService>(), serviceProvider.GetRequiredService<IConfigurationService>());
            return (int)PgDocker.Core.ExitCode.Success;

        case "info":
            await HandleInfo(args, serviceProvider.GetRequiredService<IBackupLocatorService>(), serviceProvider.GetRequiredService<IManifestService>(), serviceProvider.GetRequiredService<IConfigurationService>());
            return (int)PgDocker.Core.ExitCode.Success;

        case "upload":
            await HandleUpload(args, serviceProvider.GetRequiredService<ISftpService>(), serviceProvider.GetRequiredService<IConfigurationService>(), serviceProvider.GetRequiredService<IBackupLocatorService>());
            return (int)PgDocker.Core.ExitCode.Success;

        case "download":
            await HandleDownload(args, serviceProvider.GetRequiredService<ISftpService>());
            return (int)PgDocker.Core.ExitCode.Success;

        case "prune":
            await HandlePrune(args, serviceProvider.GetRequiredService<IRetentionService>());
            return (int)PgDocker.Core.ExitCode.Success;

        case "--help" or "-h" or "help":
            ShowHelp();
            return (int)PgDocker.Core.ExitCode.Success;

        default:
            Console.Error.WriteLine($"Unknown command: {command}");
            ShowHelp();
            return (int)PgDocker.Core.ExitCode.GeneralError;
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Application error");
    return (int)PgDocker.Core.ExitCode.GeneralError;
}
finally
{
    await Log.CloseAndFlushAsync();
}

void ShowHelp()
{
    Console.WriteLine("pgdocker - PostgreSQL Docker Backup Tool");
    Console.WriteLine();
    Console.WriteLine("Usage: pgdocker [command] [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  backup             Create a backup of PostgreSQL databases");
    Console.WriteLine("  restore            Restore PostgreSQL databases from a backup");
    Console.WriteLine("  verify             Verify the integrity of a backup");
    Console.WriteLine("  list               List available backups");
    Console.WriteLine("  info               Show information about a backup");
    Console.WriteLine("  upload             Upload a backup to SFTP server");
    Console.WriteLine("  download           Download a backup from SFTP server");
    Console.WriteLine("  prune              Remove old backups according to retention policy");
    Console.WriteLine("  help               Show this help message");
    Console.WriteLine();
    Console.WriteLine("Common Options:");
    Console.WriteLine("  -c, --config       Path to configuration file (default: pgdocker.yml)");
    Console.WriteLine("  -v, --verbose      Show detailed debug logs");
    Console.WriteLine("  -q, --quiet        Suppress non-critical logs");
    Console.WriteLine();
    Console.WriteLine("Backup Options:");
    Console.WriteLine("  -d, --database     Backup specific database only");
    Console.WriteLine("  -u, --upload       Upload backup to SFTP after completion");
    Console.WriteLine("  -p, --prune        Apply retention policy after backup");
    Console.WriteLine();
    Console.WriteLine("Restore Options:");
    Console.WriteLine("  -d, --database     Specific database to restore");
    Console.WriteLine("  --clean            Drop database before restore");
    Console.WriteLine("  -y, --yes          Skip confirmation prompts");
    Console.WriteLine();
    Console.WriteLine("Upload Options:");
    Console.WriteLine("  [backup-name]      Name of backup to upload (default: latest)");
    Console.WriteLine();
    Console.WriteLine("Download Options:");
    Console.WriteLine("  [backup-name]      Name of backup to download (required)");
    Console.WriteLine();
    Console.WriteLine("Prune Options:");
    Console.WriteLine("  --dry-run          Show what would be deleted without deleting");
}

async Task HandleBackup(string[] args, IBackupService backupService)
{
    var configPath = "pgdocker.yml";
    var upload = false;
    var prune = false;
    var databaseName = "";

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-c":
            case "--config":
                if (i + 1 < args.Length)
                    configPath = args[++i];
                break;
            case "-d":
            case "--database":
                if (i + 1 < args.Length)
                    databaseName = args[++i];
                break;
            case "-u":
            case "--upload":
                upload = true;
                break;
            case "-p":
            case "--prune":
                prune = true;
                break;
        }
    }

    try
    {
        var backupPath = await backupService.ExecuteBackupAsync(configPath, upload, prune, string.IsNullOrEmpty(databaseName) ? null : databaseName);
        Console.WriteLine($"✓ Backup created at: {backupPath}");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Backup failed");
        Console.Error.WriteLine($"✗ Backup failed: {ex.Message}");
        Environment.Exit((int)PgDocker.Core.ExitCode.BackupFailed);
    }
}

async Task HandleRestore(string[] args, IRestoreService restoreService)
{
    var configPath = "pgdocker.yml";
    var backupName = "latest";
    var databaseName = "";
    var clean = false;
    var yes = false;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-c":
            case "--config":
                if (i + 1 < args.Length)
                    configPath = args[++i];
                break;
            case "-d":
            case "--database":
                if (i + 1 < args.Length)
                    databaseName = args[++i];
                break;
            case "--clean":
                clean = true;
                break;
            case "-y":
            case "--yes":
                yes = true;
                break;
            default:
                if (!args[i].StartsWith("-"))
                    backupName = args[i];
                break;
        }
    }

    try
    {
        if (clean && !yes)
        {
            Console.WriteLine("WARNING:");
            if (string.IsNullOrEmpty(databaseName))
            {
                Console.WriteLine("All databases will be cleaned before restore.");
            }
            else
            {
                Console.WriteLine($"Database '{databaseName}' will be cleaned before restore.");
            }
            Console.Write("\nContinue? [y/N] ");
            var response = Console.ReadLine()?.ToLower();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Restore cancelled");
                return;
            }
        }

        await restoreService.ExecuteRestoreAsync(configPath, backupName, string.IsNullOrEmpty(databaseName) ? null : databaseName, clean, yes);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Restore failed");
        Console.Error.WriteLine($"✗ Restore failed: {ex.Message}");
        Environment.Exit((int)PgDocker.Core.ExitCode.RestoreFailed);
    }
}

async Task HandleVerify(string[] args, IVerifyService verifyService)
{
    var configPath = "pgdocker.yml";
    var backupName = "latest";

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-c":
            case "--config":
                if (i + 1 < args.Length)
                    configPath = args[++i];
                break;
            default:
                if (!args[i].StartsWith("-"))
                    backupName = args[i];
                break;
        }
    }

    try
    {
        var isValid = await verifyService.VerifyBackupAsync(configPath, backupName);
        Environment.Exit(isValid ? (int)PgDocker.Core.ExitCode.Success : (int)PgDocker.Core.ExitCode.IntegrityVerificationFailed);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Verification failed");
        Console.Error.WriteLine($"✗ Verification failed: {ex.Message}");
        Environment.Exit((int)PgDocker.Core.ExitCode.IntegrityVerificationFailed);
    }
}

async Task HandleList(string[] args, IBackupLocatorService locator, IConfigurationService configService)
{
    var configPath = "pgdocker.yml";

    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "-c" || args[i] == "--config")
        {
            if (i + 1 < args.Length)
                configPath = args[++i];
        }
    }

    try
    {
        var config = await configService.LoadConfigurationAsync(configPath);
        var backups = await locator.ListBackupsAsync(config.Backup.Path);

        if (backups.Count == 0)
        {
            Console.WriteLine("No backups found");
            return;
        }

        Console.WriteLine($"\n{"Name",-20} {"CreatedAt",-20} {"Size",-10} {"Compressed"}");
        Console.WriteLine(new string('-', 60));

        foreach (var backup in backups)
        {
            var sizeStr = FormatBytes(backup.SizeBytes);
            var compressedStr = backup.IsCompressed ? "Yes" : "No";
            Console.WriteLine($"{backup.Name,-20} {backup.CreatedAt:yyyy-MM-dd HH:mm:ss,-20} {sizeStr,-10} {compressedStr}");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "List failed");
        Console.Error.WriteLine($"✗ List failed: {ex.Message}");
        Environment.Exit((int)PgDocker.Core.ExitCode.GeneralError);
    }
}

async Task HandleInfo(string[] args, IBackupLocatorService locator, IManifestService manifestService, IConfigurationService configService)
{
    var configPath = "pgdocker.yml";
    var backupName = "latest";

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-c":
            case "--config":
                if (i + 1 < args.Length)
                    configPath = args[++i];
                break;
            default:
                if (!args[i].StartsWith("-"))
                    backupName = args[i];
                break;
        }
    }

    try
    {
        var config = await configService.LoadConfigurationAsync(configPath);
        var backupDir = await locator.ResolveBackupDirectoryAsync(config.Backup.Path, backupName);
        var manifest = await manifestService.ReadManifestAsync(backupDir);

        var dirInfo = new DirectoryInfo(backupDir);
        var size = dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);

        Console.WriteLine($"\nBackup: {backupName}");
        Console.WriteLine($"Created: {manifest.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Tool Version: {manifest.ToolVersion}");
        Console.WriteLine($"Container: {manifest.Container}");
        Console.WriteLine($"PostgreSQL Version: {manifest.PostgresVersion}");
        Console.WriteLine($"Size: {FormatBytes(size)}");
        Console.WriteLine($"Databases: {manifest.Databases.Count}");

        if (manifest.Databases.Count > 0)
        {
            Console.WriteLine("\n  Databases:");
            foreach (var db in manifest.Databases)
            {
                Console.WriteLine($"    - {db.Name} ({db.File})");
            }
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Info failed");
        Console.Error.WriteLine($"✗ Info failed: {ex.Message}");
        Environment.Exit((int)PgDocker.Core.ExitCode.GeneralError);
    }
}

async Task HandlePrune(string[] args, IRetentionService retentionService)
{
    var configPath = "pgdocker.yml";
    var dryRun = false;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-c":
            case "--config":
                if (i + 1 < args.Length)
                    configPath = args[++i];
                break;
            case "--dry-run":
                dryRun = true;
                break;
        }
    }

    try
    {
        await retentionService.PruneBackupsAsync(configPath, dryRun);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Prune failed");
        Console.Error.WriteLine($"✗ Prune failed: {ex.Message}");
        Environment.Exit((int)PgDocker.Core.ExitCode.RetentionFailed);
    }
}

async Task HandleUpload(string[] args, ISftpService sftpService, IConfigurationService configService, IBackupLocatorService backupLocator)
{
    var configPath = "pgdocker.yml";
    var backupName = "latest";

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-c":
            case "--config":
                if (i + 1 < args.Length)
                    configPath = args[++i];
                break;
            default:
                if (!args[i].StartsWith("-"))
                    backupName = args[i];
                break;
        }
    }

    try
    {
        var config = await configService.LoadConfigurationAsync(configPath);
        var backupPath = await backupLocator.ResolveBackupDirectoryAsync(config.Backup.Path, backupName);

        await sftpService.UploadBackupAsync(backupPath, configPath);
        Console.WriteLine($"✓ Backup uploaded to SFTP server");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Upload failed");
        Console.Error.WriteLine($"✗ Upload failed: {ex.Message}");
        Environment.Exit((int)PgDocker.Core.ExitCode.SftpFailed);
    }
}

async Task HandleDownload(string[] args, ISftpService sftpService)
{
    var configPath = "pgdocker.yml";
    var backupName = "";

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-c":
            case "--config":
                if (i + 1 < args.Length)
                    configPath = args[++i];
                break;
            default:
                if (!args[i].StartsWith("-"))
                    backupName = args[i];
                break;
        }
    }

    if (string.IsNullOrWhiteSpace(backupName))
    {
        Console.Error.WriteLine("✗ Backup name is required for download");
        Console.Error.WriteLine("Usage: pgdocker download <backup-name> [-c config.yml]");
        Environment.Exit((int)PgDocker.Core.ExitCode.GeneralError);
    }

    try
    {
        await sftpService.DownloadBackupAsync(backupName, configPath);
        Console.WriteLine($"✓ Backup {backupName} downloaded from SFTP server");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Download failed");
        Console.Error.WriteLine($"✗ Download failed: {ex.Message}");
        Environment.Exit((int)PgDocker.Core.ExitCode.SftpFailed);
    }
}

string FormatBytes(long bytes)
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
