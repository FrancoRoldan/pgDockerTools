namespace PgDocker.Core.Interfaces;

public interface ICompressionService
{
    Task CompressDirectoryAsync(string directory);
    Task<string> GetCompressedFileName(string directory);
    Task ExtractArchiveAsync(string archivePath, string? targetDirectory = null);
}
