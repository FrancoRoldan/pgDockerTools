namespace PgDocker.Core.Interfaces;

public interface IRetentionService
{
    Task PruneBackupsAsync(string configPath, bool dryRun = false);
}
