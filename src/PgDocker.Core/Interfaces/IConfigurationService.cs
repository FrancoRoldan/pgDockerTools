namespace PgDocker.Core.Interfaces;

using Models;

public interface IConfigurationService
{
    Task<PgDockerConfig> LoadConfigurationAsync(string configPath);
    void ValidateConfiguration(PgDockerConfig config);
}
