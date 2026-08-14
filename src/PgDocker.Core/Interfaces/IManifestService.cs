namespace PgDocker.Core.Interfaces;

using Models;

public interface IManifestService
{
    Task WriteManifestAsync(string directory, BackupManifest manifest);
    Task<BackupManifest> ReadManifestAsync(string directory);
}
