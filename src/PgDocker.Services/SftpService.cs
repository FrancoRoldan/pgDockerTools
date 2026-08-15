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
        var isCompressed = Directory.GetFiles(backupPath).Any(f =>
            f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));

        var fileToUpload = isCompressed
            ? Directory.GetFiles(backupPath).First(f =>
                f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            : backupPath;

        using (var client = CreateSftpClient(config.Sftp))
        {
            client.Connect();
            _logger.Information("Connected to SFTP server {Host}:{Port}", config.Sftp.Host, config.Sftp.Port);

            EnsureRemoteDirectory(client, config.Sftp.RemotePath);

            var remotePath = Path.Combine(config.Sftp.RemotePath, Path.GetFileName(fileToUpload))
                .Replace("\\", "/");

            using (var fileStream = new FileStream(fileToUpload, FileMode.Open, FileAccess.Read))
            {
                client.UploadFile(fileStream, remotePath);
                _logger.Information("Uploaded {FileName} to {RemotePath}", Path.GetFileName(fileToUpload), remotePath);
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
                    client.UploadFile(fileStream, remoteManifest);
                    _logger.Information("Uploaded manifest to {RemotePath}", remoteManifest);
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
                    client.UploadFile(fileStream, remoteSha);
                    _logger.Information("Uploaded checksum to {RemotePath}", remoteSha);
                }
            }

            client.Disconnect();
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
        var connectionInfo = config.Authentication?.ToLower() == "key"
            ? CreateKeyAuthenticationInfo(config)
            : throw new InvalidOperationException("Only key-based authentication is currently supported");

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

    private void ValidateSftpConfig(SftpConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
            throw new InvalidOperationException("SFTP host is not configured");

        if (string.IsNullOrWhiteSpace(config.Username))
            throw new InvalidOperationException("SFTP username is not configured");

        if (string.IsNullOrWhiteSpace(config.RemotePath))
            throw new InvalidOperationException("SFTP remote path is not configured");

        if (string.IsNullOrWhiteSpace(config.PrivateKey))
            throw new InvalidOperationException("SFTP private key path is not configured");

        var expandedKeyPath = ExpandPath(config.PrivateKey);
        if (!File.Exists(expandedKeyPath))
            throw new InvalidOperationException($"Private key file not found: {expandedKeyPath}");
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
        return path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }
}
