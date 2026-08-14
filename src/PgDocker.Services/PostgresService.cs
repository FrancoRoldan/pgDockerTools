namespace PgDocker.Services;

using PgDocker.Core.Interfaces;

public class PostgresService : IPostgresService
{
    private readonly IDockerService _dockerService;

    public PostgresService(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<string> GetPostgresVersionAsync(string containerName, string username)
    {
        try
        {
            var output = await _dockerService.ExecuteCommandAndGetOutputAsync(
                containerName,
                "psql",
                new[] { "-U", username, "-d", "postgres", "--version" }
            );

            var versionParts = output.Split(' ');
            foreach (var part in versionParts)
            {
                if (Version.TryParse(part, out _))
                    return part;
            }

            return output;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get PostgreSQL version: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> GetDatabaseListAsync(string containerName, string username, List<string> excludeDatabases)
    {
        try
        {
            var query = "select datname from pg_database where not datistemplate";
            var output = await _dockerService.ExecuteCommandAndGetOutputAsync(
                containerName,
                "psql",
                new[] { "-U", username, "-d", "postgres", "-tAc", query }
            );

            var databases = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(db => db.Trim())
                .Where(db => !string.IsNullOrWhiteSpace(db) && !excludeDatabases.Contains(db))
                .ToList();

            return databases;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get database list: {ex.Message}", ex);
        }
    }
}
