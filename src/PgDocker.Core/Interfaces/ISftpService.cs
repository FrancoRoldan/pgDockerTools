namespace PgDocker.Core.Interfaces;

public interface ISftpService
{
    Task UploadBackupAsync(string backupPath);
    Task DownloadBackupAsync(string backupName);
}
