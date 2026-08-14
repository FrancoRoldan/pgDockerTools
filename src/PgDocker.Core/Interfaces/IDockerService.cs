namespace PgDocker.Core.Interfaces;

public interface IDockerService
{
    Task<bool> IsDockerAvailableAsync();
    Task<bool> ContainerExistsAsync(string containerName);
    Task<bool> ContainerIsRunningAsync(string containerName);
    Task<Stream> ExecuteCommandAsync(string containerName, string command, string[] args);
    Task<string> ExecuteCommandAndGetOutputAsync(string containerName, string command, string[] args);
    Task<string> ExecuteCommandWithInputAsync(string containerName, string command, string[] args, Stream inputStream);
}
