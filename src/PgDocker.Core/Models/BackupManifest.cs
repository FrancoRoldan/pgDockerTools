namespace PgDocker.Core.Models;

public class BackupManifest
{
    public string ToolVersion { get; set; } = "1.0.0";
    public DateTime CreatedAt { get; set; }
    public string Container { get; set; } = string.Empty;
    public string PostgresVersion { get; set; } = string.Empty;
    public List<DatabaseEntry> Databases { get; set; } = new();
}

public class DatabaseEntry
{
    public string Name { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
}
