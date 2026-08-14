namespace PgDocker.Core.Interfaces;

public interface IRestoreService
{
    Task ExecuteRestoreAsync(string configPath, string backupName, string? databaseName = null, bool clean = false, bool yes = false);
}
