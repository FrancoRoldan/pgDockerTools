namespace PgDocker.Services;

using PgDocker.Core.Interfaces;

public class SftpService : ISftpService
{
    public Task DownloadBackupAsync(string backupName)
    {
        throw new NotImplementedException("SFTP download functionality will be implemented in the next iteration");
    }

    public Task UploadBackupAsync(string backupPath)
    {
        throw new NotImplementedException("SFTP upload functionality will be implemented in the next iteration");
    }
}
