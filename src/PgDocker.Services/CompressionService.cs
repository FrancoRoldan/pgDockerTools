namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

public class CompressionService : ICompressionService
{
    public async Task CompressDirectoryAsync(string directory)
    {
        var directoryName = Path.GetFileName(directory);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await CompressAsZipAsync(directory, directoryName);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await CompressAsTarGzAsync(directory, directoryName);
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported platform for compression");
        }
    }

    public Task<string> GetCompressedFileName(string directory)
    {
        var directoryName = Path.GetFileName(directory);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult($"{directoryName}.zip");
        }
        else
        {
            return Task.FromResult($"{directoryName}.tar.gz");
        }
    }

    private async Task CompressAsZipAsync(string directory, string directoryName)
    {
        var parentDir = Directory.GetParent(directory)!.FullName;
        var zipPath = Path.Combine(parentDir, $"{directoryName}.zip");

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        await Task.Run(() =>
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    archive.CreateEntryFromFile(file, Path.GetFileName(file));
                }
            }
        });
    }

    private async Task CompressAsTarGzAsync(string directory, string directoryName)
    {
        var parentDir = Directory.GetParent(directory)!.FullName;
        var tarGzPath = Path.Combine(parentDir, $"{directoryName}.tar.gz");

        if (File.Exists(tarGzPath))
            File.Delete(tarGzPath);

        var processInfo = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-czf {tarGzPath} -C {parentDir} {directoryName}",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = Process.Start(processInfo))
        {
            await process!.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Failed to create tar.gz archive");
            }
        }
    }

    public async Task ExtractArchiveAsync(string archivePath, string? targetDirectory = null)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"Archive not found: {archivePath}");

        var targetDir = targetDirectory ?? Path.GetDirectoryName(archivePath)!;

        if (archivePath.EndsWith(".zip"))
        {
            await Task.Run(() =>
            {
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var entryPath = Path.Combine(targetDir, entry.FullName);
                        var entryDir = Path.GetDirectoryName(entryPath)!;
                        Directory.CreateDirectory(entryDir);

                        if (!entry.FullName.EndsWith("/"))
                        {
                            entry.ExtractToFile(entryPath, true);
                        }
                    }
                }
            });
        }
        else if (archivePath.EndsWith(".tar.gz"))
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf {archivePath} -C {targetDir}",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                await process!.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("Failed to extract tar.gz archive");
                }
            }
        }
        else
        {
            throw new NotSupportedException($"Archive format not supported: {Path.GetExtension(archivePath)}");
        }
    }
}
