namespace PgDocker.Core.Interfaces;

public interface IVerifyService
{
    Task<bool> VerifyBackupAsync(string configPath, string backupName);
}
