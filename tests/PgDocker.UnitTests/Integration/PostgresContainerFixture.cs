using Testcontainers.PostgreSql;

namespace PgDocker.UnitTests.Integration;

public class PostgresContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public string? ContainerName { get; private set; }
    public string? ConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        var containerName = $"pgdocker-test-{Guid.NewGuid().ToString()[..8]}";

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithName(containerName)
            .WithUsername("postgres")
            .WithPassword("test-password")
            .WithDatabase("postgres")
            .Build();

        await _container.StartAsync();

        ContainerName = containerName;
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.StopAsync();
    }
}
