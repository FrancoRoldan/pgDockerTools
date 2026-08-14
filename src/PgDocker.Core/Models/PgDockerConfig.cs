namespace PgDocker.Core.Models;

public class PgDockerConfig
{
    public PostgresConfig Postgres { get; set; } = new();
    public BackupConfig Backup { get; set; } = new();
    public RetentionConfig Retention { get; set; } = new();
    public SftpConfig Sftp { get; set; } = new();
}

public class PostgresConfig
{
    public string Container { get; set; } = "postgres";
    public string Username { get; set; } = "postgres";
}

public class BackupConfig
{
    public string Path { get; set; } = "./backups";
    public bool Compression { get; set; } = true;
    public bool Verify { get; set; } = true;
    public List<string> ExcludeDatabases { get; set; } = new() { "template0", "template1" };
}

public class RetentionConfig
{
    public int LocalDays { get; set; } = 30;
    public int RemoteDays { get; set; } = 90;
}

public class SftpConfig
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string RemotePath { get; set; } = string.Empty;
    public string Authentication { get; set; } = "key";
    public string PrivateKey { get; set; } = string.Empty;
}
