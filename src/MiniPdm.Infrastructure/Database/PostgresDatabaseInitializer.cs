using System.Reflection;
using Dapper;
using MiniPdm.Application.Persistence;

namespace MiniPdm.Infrastructure.Database;

/// <summary>
/// Применяет скрипт схемы из репозитория при старте. Скрипт идемпотентен
/// (<c>CREATE ... IF NOT EXISTS</c>), поэтому повторный запуск безопасен.
/// </summary>
public sealed class PostgresDatabaseInitializer : IDatabaseInitializer
{
    private const string ResourceName = "MiniPdm.Infrastructure.Database.Schema.sql";

    private readonly IDbConnectionFactory _factory;

    public PostgresDatabaseInitializer(IDbConnectionFactory factory)
        => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public async Task InitializeAsync(CancellationToken ct)
    {
        var script = await ReadScriptAsync().ConfigureAwait(false);
        await using var connection = await _factory.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: ct)).ConfigureAwait(false);
    }

    private static async Task<string> ReadScriptAsync()
    {
        var assembly = typeof(PostgresDatabaseInitializer).Assembly;
        await using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Не найден встроенный ресурс «{ResourceName}».");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}
