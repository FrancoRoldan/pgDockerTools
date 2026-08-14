namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using System.Security.Cryptography;

public class HashService : IHashService
{
    public async Task<string> CalculateFileHashAsync(string filePath)
    {
        using (var sha256 = SHA256.Create())
        using (var fileStream = File.OpenRead(filePath))
        {
            var hash = await Task.Run(() => sha256.ComputeHash(fileStream));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    public async Task WriteSha256FileAsync(string directory, Dictionary<string, string> fileHashes)
    {
        var sha256FilePath = Path.Combine(directory, "sha256.txt");
        var lines = fileHashes.Select(kvp => $"{kvp.Value}  {Path.GetFileName(kvp.Key)}");
        await File.WriteAllLinesAsync(sha256FilePath, lines);
    }

    public async Task<Dictionary<string, string>> ReadSha256FileAsync(string directory)
    {
        var sha256FilePath = Path.Combine(directory, "sha256.txt");
        if (!File.Exists(sha256FilePath))
            return new();

        var hashes = new Dictionary<string, string>();
        var lines = await File.ReadAllLinesAsync(sha256FilePath);

        foreach (var line in lines)
        {
            var parts = line.Split(new[] { "  ", "\t" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var hash = parts[0].Trim();
                var fileName = parts[1].Trim();
                hashes[fileName] = hash;
            }
        }

        return hashes;
    }
}
