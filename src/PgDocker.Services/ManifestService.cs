namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using PgDocker.Core.Models;
using System.Text.Json;

public class ManifestService : IManifestService
{
    public async Task WriteManifestAsync(string directory, BackupManifest manifest)
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json);
    }

    public async Task<BackupManifest> ReadManifestAsync(string directory)
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Manifest file not found: {manifestPath}");

        var json = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(json) ?? throw new InvalidOperationException("Failed to deserialize manifest");
        return manifest;
    }
}
