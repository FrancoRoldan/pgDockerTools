namespace PgDocker.Core.Interfaces;

public interface IPostgresService
{
    Task<string> GetPostgresVersionAsync(string containerName, string username);
    Task<List<string>> GetDatabaseListAsync(string containerName, string username, List<string> excludeDatabases);
}
