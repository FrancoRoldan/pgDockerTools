namespace PgDocker.Services;

using PgDocker.Core.Interfaces;
using PgDocker.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class ConfigurationService : IConfigurationService
{
    public async Task<PgDockerConfig> LoadConfigurationAsync(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Configuration file not found: {configPath}");

            var yaml = await File.ReadAllTextAsync(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<PgDockerConfig>(yaml) ?? new PgDockerConfig();

            // Apply environment variable overrides
            ApplyEnvironmentOverrides(config);

            ValidateConfiguration(config);
            return config;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load configuration: {ex.Message}", ex);
        }
    }

    public void ValidateConfiguration(PgDockerConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Postgres.Container))
            throw new InvalidOperationException("Postgres container name is required");

        if (string.IsNullOrWhiteSpace(config.Postgres.Username))
            throw new InvalidOperationException("Postgres username is required");

        if (string.IsNullOrWhiteSpace(config.Backup.Path))
            throw new InvalidOperationException("Backup path is required");
    }

    private void ApplyEnvironmentOverrides(PgDockerConfig config)
    {
        var sftp_host = Environment.GetEnvironmentVariable("PGDOCKER_SFTP_HOST");
        var sftp_user = Environment.GetEnvironmentVariable("PGDOCKER_SFTP_USER");
        var sftp_key = Environment.GetEnvironmentVariable("PGDOCKER_SFTP_PRIVATE_KEY");

        if (!string.IsNullOrEmpty(sftp_host)) config.Sftp.Host = sftp_host;
        if (!string.IsNullOrEmpty(sftp_user)) config.Sftp.Username = sftp_user;
        if (!string.IsNullOrEmpty(sftp_key)) config.Sftp.PrivateKey = sftp_key;
    }
}
