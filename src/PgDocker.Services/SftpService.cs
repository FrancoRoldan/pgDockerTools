namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using PgDocker.Core.Models;
using Renci.SshNet;
using Serilog;

public class SftpService : ISftpService
{
    private readonly IConfigurationService _configService;
    private readonly IBackupLocatorService _backupLocator;
    private readonly ILogger _logger;

    public SftpService(
        IConfigurationService configService,
        IBackupLocatorService backupLocator,
        ILogger logger)
    {
        _configService = configService;
        _backupLocator = backupLocator;
        _logger = logger;
    }

    public async Task UploadBackupAsync(string backupPath, string? configPath = null)
    {
        configPath ??= "pgdocker.yml";
        var config = await _configService.LoadConfigurationAsync(configPath);

        if (!config.Sftp.Enabled)
            throw new InvalidOperationException("SFTP is not enabled in configuration");

        ValidateSftpConfig(config.Sftp);

        _logger.Information("Starting SFTP upload for {BackupPath}", backupPath);

        var backupName = new DirectoryInfo(backupPath).Name;
        var parentDir = Path.GetDirectoryName(backupPath);
        var compressedFileWithZip = Path.Combine(parentDir ?? "", backupName + ".zip");
        var compressedFileWithTarGz = Path.Combine(parentDir ?? "", backupName + ".tar.gz");

        string? compressedFile = null;
        if (File.Exists(compressedFileWithZip))
            compressedFile = compressedFileWithZip;
        else if (File.Exists(compressedFileWithTarGz))
            compressedFile = compressedFileWithTarGz;

        var isCompressed = compressedFile != null;

        using (var client = CreateSftpClient(config.Sftp))
        {
            client.Connect();
            _logger.Information("Connected to SFTP server {Host}:{Port}", config.Sftp.Host, config.Sftp.Port);

            EnsureRemoteDirectory(client, config.Sftp.RemotePath);

            if (isCompressed && compressedFile != null)
            {
                var fileName = Path.GetFileName(compressedFile);
                var fileInfo = new FileInfo(compressedFile);
                var fileSize = fileInfo.Length;
                var remotePath = Path.Combine(config.Sftp.RemotePath, fileName)
                    .Replace("\\", "/");

                _logger.Information("Uploading compressed backup: {LocalPath} ({SizeKB}KB) exists: {Exists}",
                    fileInfo.FullName, fileSize / 1024, fileInfo.Exists);
                _logger.Debug("Remote destination: {RemotePath}", remotePath);

                using (var fileStream = new FileStream(compressedFile, FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        client.UploadFile(fileStream, remotePath);
                        _logger.Information("Successfully uploaded {FileName} ({SizeKB}KB) to {RemotePath}",
                            fileName, fileSize / 1024, remotePath);

                        try
                        {
                            if (client.Exists(remotePath))
                            {
                                var attributes = client.GetAttributes(remotePath);
                                _logger.Information("Verified remote file exists: {RemotePath} (size: {RemoteSizeKB}KB)",
                                    remotePath, attributes.Size / 1024);
                            }
                            else
                            {
                                _logger.Warning("Remote file verification failed: {RemotePath} does not exist on server", remotePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(ex, "Could not verify remote file: {RemotePath}", remotePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to upload {FileName} to {RemotePath}", fileName, remotePath);
                        throw;
                    }
                }
            }

            var remoteBackupDir = Path.Combine(config.Sftp.RemotePath, backupName).Replace("\\", "/");
            EnsureRemoteDirectory(client, remoteBackupDir);

            var sqlFilesToUpload = Directory.GetFiles(backupPath).Where(f =>
                f.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".dump", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var file in sqlFilesToUpload)
            {
                var fileName = Path.GetFileName(file);
                var fileSize = new FileInfo(file).Length;
                var remotePath = Path.Combine(remoteBackupDir, fileName).Replace("\\", "/");

                _logger.Debug("Uploading {FileName} ({SizeKB}KB) to {RemotePath}", fileName, fileSize / 1024, remotePath);
                using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        client.UploadFile(fileStream, remotePath);
                        _logger.Information("Successfully uploaded {FileName} ({SizeKB}KB) to {RemotePath}",
                            fileName, fileSize / 1024, remotePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to upload {FileName} to {RemotePath}", fileName, remotePath);
                        throw;
                    }
                }
            }

            var manifestFile = Path.Combine(backupPath, "manifest.json");
            if (File.Exists(manifestFile))
            {
                var remoteManifest = Path.Combine(config.Sftp.RemotePath, backupName, "manifest.json")
                    .Replace("\\", "/");
                var remoteDir = Path.GetDirectoryName(remoteManifest)?.Replace("\\", "/") ?? config.Sftp.RemotePath;
                EnsureRemoteDirectory(client, remoteDir);

                using (var fileStream = new FileStream(manifestFile, FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        client.UploadFile(fileStream, remoteManifest);
                        _logger.Information("Successfully uploaded manifest to {RemotePath}", remoteManifest);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to upload manifest to {RemotePath}", remoteManifest);
                        throw;
                    }
                }
            }

            var sha256File = Path.Combine(backupPath, "sha256.txt");
            if (File.Exists(sha256File))
            {
                var remoteSha = Path.Combine(config.Sftp.RemotePath, backupName, "sha256.txt")
                    .Replace("\\", "/");
                var remoteDir = Path.GetDirectoryName(remoteSha)?.Replace("\\", "/") ?? config.Sftp.RemotePath;
                EnsureRemoteDirectory(client, remoteDir);

                using (var fileStream = new FileStream(sha256File, FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        client.UploadFile(fileStream, remoteSha);
                        _logger.Information("Successfully uploaded checksum to {RemotePath}", remoteSha);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to upload checksum to {RemotePath}", remoteSha);
                        throw;
                    }
                }
            }

            try
            {
                client.Disconnect();
                _logger.Debug("SFTP connection closed gracefully");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error closing SFTP connection");
            }
        }

        _logger.Information("SFTP upload completed successfully");
    }

    public async Task DownloadBackupAsync(string backupName, string? configPath = null)
    {
        configPath ??= "pgdocker.yml";
        var config = await _configService.LoadConfigurationAsync(configPath);

        if (!config.Sftp.Enabled)
            throw new InvalidOperationException("SFTP is not enabled in configuration");

        ValidateSftpConfig(config.Sftp);

        _logger.Information("Starting SFTP download for {BackupName}", backupName);

        var localBackupPath = Path.Combine(config.Backup.Path, backupName);
        Directory.CreateDirectory(localBackupPath);

        using (var client = CreateSftpClient(config.Sftp))
        {
            client.Connect();
            _logger.Information("Connected to SFTP server {Host}:{Port}", config.Sftp.Host, config.Sftp.Port);

            var remoteBackupPath = Path.Combine(config.Sftp.RemotePath, backupName).Replace("\\", "/");

            // List files in remote backup directory
            var remoteFiles = client.ListDirectory(remoteBackupPath);
            var filesToDownload = remoteFiles
                .Where(f => !f.IsDirectory && (f.Name.EndsWith(".zip") || f.Name.EndsWith(".tar.gz") ||
                    f.Name == "manifest.json" || f.Name == "sha256.txt"))
                .ToList();

            if (!filesToDownload.Any())
                throw new InvalidOperationException($"No backup files found in remote path {remoteBackupPath}");

            foreach (var file in filesToDownload)
            {
                var remotePath = Path.Combine(remoteBackupPath, file.Name).Replace("\\", "/");
                var localPath = Path.Combine(localBackupPath, file.Name);

                using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write))
                {
                    client.DownloadFile(remotePath, fileStream);
                    _logger.Information("Downloaded {FileName} from {RemotePath}", file.Name, remotePath);
                }
            }

            client.Disconnect();
        }

        _logger.Information("SFTP download completed successfully to {LocalPath}", localBackupPath);
    }

    private SftpClient CreateSftpClient(SftpConfig config)
    {
        var connectionInfo = config.Authentication?.ToLower() switch
        {
            "key" => CreateKeyAuthenticationInfo(config),
            "password" => CreatePasswordAuthenticationInfo(config),
            _ => throw new InvalidOperationException($"Unknown authentication method: {config.Authentication}. Supported: 'key', 'password'")
        };

        return new SftpClient(connectionInfo);
    }

    private ConnectionInfo CreateKeyAuthenticationInfo(SftpConfig config)
    {
        var privateKeyFile = new PrivateKeyFile(
            ExpandPath(config.PrivateKey),
            config.Username);

        var methods = new List<AuthenticationMethod>
        {
            new PrivateKeyAuthenticationMethod(config.Username, privateKeyFile)
        };

        return new ConnectionInfo(
            config.Host,
            config.Port,
            config.Username,
            methods.ToArray());
    }

    private ConnectionInfo CreatePasswordAuthenticationInfo(SftpConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Password))
            throw new InvalidOperationException("Password is required when using password authentication");

        var methods = new List<AuthenticationMethod>
        {
            new PasswordAuthenticationMethod(config.Username, config.Password)
        };

        return new ConnectionInfo(
            config.Host,
            config.Port,
            config.Username,
            methods.ToArray());
    }

    private void ValidateSftpConfig(SftpConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
            throw new InvalidOperationException("SFTP host is not configured");

        if (string.IsNullOrWhiteSpace(config.Username))
            throw new InvalidOperationException("SFTP username is not configured");

        if (string.IsNullOrWhiteSpace(config.RemotePath))
            throw new InvalidOperationException("SFTP remote path is not configured");

        var authMethod = config.Authentication?.ToLower() ?? "key";

        if (authMethod == "key")
        {
            if (string.IsNullOrWhiteSpace(config.PrivateKey))
                throw new InvalidOperationException("SFTP private key path is not configured");

            var expandedKeyPath = ExpandPath(config.PrivateKey);
            if (!File.Exists(expandedKeyPath))
                throw new InvalidOperationException($"Private key file not found: {expandedKeyPath}");
        }
        else if (authMethod == "password")
        {
            if (string.IsNullOrWhiteSpace(config.Password))
                throw new InvalidOperationException("SFTP password is not configured");
        }
        else
        {
            throw new InvalidOperationException($"Unknown authentication method: {config.Authentication}. Supported: 'key', 'password'");
        }
    }

    private void EnsureRemoteDirectory(SftpClient client, string remotePath)
    {
        try
        {
            client.ChangeDirectory(remotePath);
        }
        catch
        {
            // Directory doesn't exist, create it
            var parts = remotePath.Split('/');
            var currentPath = "";

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                currentPath += "/" + part;

                try
                {
                    client.ChangeDirectory(currentPath);
                }
                catch
                {
                    client.CreateDirectory(currentPath);
                    _logger.Debug("Created remote directory {Path}", currentPath);
                }
            }
        }
    }

    private string ExpandPath(string path)
    {
        var expanded = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        return Path.GetFullPath(expanded);
    }
}
