namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using PgDocker.Core.Models;
using System.Text.RegularExpressions;

public class BackupLocatorService : IBackupLocatorService
{
    private readonly ICompressionService _compressionService;

    public BackupLocatorService(ICompressionService compressionService)
    {
        _compressionService = compressionService;
    }

    public async Task<List<BackupSummary>> ListBackupsAsync(string backupPath)
    {
        if (!Directory.Exists(backupPath))
            return new();

        var backups = new Dictionary<string, BackupSummary>();

        // Find all directories matching yyyyMMdd_HHmmss pattern
        var backupDirs = Directory.GetDirectories(backupPath)
            .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^\d{8}_\d{6}$"))
            .ToList();

        foreach (var dir in backupDirs)
        {
            var dirName = Path.GetFileName(dir);
            var dirInfo = new DirectoryInfo(dir);
            var size = dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);

            // Check if there's a compressed file with the same name
            var zipPath = Path.Combine(backupPath, $"{dirName}.zip");
            var tarGzPath = Path.Combine(backupPath, $"{dirName}.tar.gz");
            var isCompressed = File.Exists(zipPath) || File.Exists(tarGzPath);

            if (DateTime.TryParseExact(dirName, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var createdAt))
            {
                backups[dirName] = new BackupSummary
                {
                    Name = dirName,
                    CreatedAt = createdAt,
                    SizeBytes = size,
                    IsCompressed = isCompressed
                };
            }
        }

        // Find compressed files without corresponding directories
        var compressedFiles = Directory.GetFiles(backupPath, "*.zip")
            .Concat(Directory.GetFiles(backupPath, "*.tar.gz"))
            .ToList();

        foreach (var file in compressedFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.EndsWith(".tar"))
                fileName = fileName.Substring(0, fileName.Length - 4);

            // Only add if directory doesn't exist
            if (!backups.ContainsKey(fileName) && Regex.IsMatch(fileName, @"^\d{8}_\d{6}$"))
            {
                var fileInfo = new FileInfo(file);
                if (DateTime.TryParseExact(fileName, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var createdAt))
                {
                    backups[fileName] = new BackupSummary
                    {
                        Name = fileName,
                        CreatedAt = createdAt,
                        SizeBytes = fileInfo.Length,
                        IsCompressed = true
                    };
                }
            }
        }

        return backups.Values.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public async Task<string> ResolveBackupDirectoryAsync(string backupPath, string nameOrLatest)
    {
        if (!Directory.Exists(backupPath))
            throw new DirectoryNotFoundException($"Backup path not found: {backupPath}");

        var backups = await ListBackupsAsync(backupPath);
        if (backups.Count == 0)
            throw new InvalidOperationException("No backups found");

        var target = nameOrLatest.ToLower() == "latest" ? backups[0].Name : nameOrLatest;

        // Check if directory exists
        var dirPath = Path.Combine(backupPath, target);
        if (Directory.Exists(dirPath))
            return dirPath;

        // Try to extract from compressed file
        var zipPath = Path.Combine(backupPath, $"{target}.zip");
        var tarGzPath = Path.Combine(backupPath, $"{target}.tar.gz");

        if (File.Exists(zipPath))
        {
            await _compressionService.ExtractArchiveAsync(zipPath, backupPath);
            return dirPath;
        }

        if (File.Exists(tarGzPath))
        {
            await _compressionService.ExtractArchiveAsync(tarGzPath, backupPath);
            return dirPath;
        }

        throw new FileNotFoundException($"Backup not found: {target}");
    }
}
