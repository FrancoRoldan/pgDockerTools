namespace PgDocker.Core.Interfaces;

using Models;

public interface IBackupLocatorService
{
    Task<List<BackupSummary>> ListBackupsAsync(string backupPath);
    Task<string> ResolveBackupDirectoryAsync(string backupPath, string nameOrLatest);
}
