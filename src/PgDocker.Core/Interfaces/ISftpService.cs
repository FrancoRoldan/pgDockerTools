namespace PgDocker.Core.Interfaces;

public interface ISftpService
{
    Task UploadBackupAsync(string backupPath, string? configPath = null);
    Task DownloadBackupAsync(string backupName, string? configPath = null);
}
