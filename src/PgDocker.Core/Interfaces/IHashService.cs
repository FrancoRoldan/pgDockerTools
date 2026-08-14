namespace PgDocker.Core.Interfaces;

public interface IHashService
{
    Task<string> CalculateFileHashAsync(string filePath);
    Task WriteSha256FileAsync(string directory, Dictionary<string, string> fileHashes);
    Task<Dictionary<string, string>> ReadSha256FileAsync(string directory);
}
