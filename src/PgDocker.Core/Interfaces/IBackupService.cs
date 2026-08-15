namespace PgDocker.Core.Interfaces;

public interface IBackupService
{
    Task<string> ExecuteBackupAsync(string configPath, bool upload = false, bool prune = false, string? databaseName = null);
}
