namespace PgDocker.Core.Models;

public class BackupSummary
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public bool IsCompressed { get; set; }
}
