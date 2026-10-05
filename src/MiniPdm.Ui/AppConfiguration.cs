using System.IO;
using System.Text.Json;

namespace MiniPdm.Ui;

/// <summary>
/// Конфигурация приложения. Строка подключения берётся из переменной окружения
/// <c>MINIPDM_CONNECTION</c> или из <c>appsettings.json</c> рядом с исполняемым файлом.
/// </summary>
public static class AppConfiguration
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=minipdm;Username=minipdm;Password=minipdm";

    public static string ConnectionString { get; } = ResolveConnectionString();

    private static string ResolveConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("MINIPDM_CONNECTION");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(path))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("ConnectionStrings", out var connections)
                    && connections.TryGetProperty("Postgres", out var postgres))
                {
                    var value = postgres.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch (JsonException)
            {
                // Некорректный файл — используем значение по умолчанию.
            }
        }

        return DefaultConnectionString;
    }
}
