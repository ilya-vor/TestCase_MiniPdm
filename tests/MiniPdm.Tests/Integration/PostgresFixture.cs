using Npgsql;
using Testcontainers.PostgreSql;

namespace MiniPdm.Tests.Integration;

/// <summary>
/// Одноразовый контейнер PostgreSQL для интеграционных тестов. Если Docker недоступен,
/// тесты помечаются как недоступные и просто не выполняются.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public bool Available { get; private set; }

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine")
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            Available = true;
        }
        catch (Exception)
        {
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public NpgsqlConnection CreateConnection() => new(ConnectionString);
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
