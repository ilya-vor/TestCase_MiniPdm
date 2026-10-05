using System.Windows;
using MiniPdm.Application.Persistence;

namespace MiniPdm.Ui;

/// <summary>Точка входа WPF. Композиция зависимостей выполняется контейнером Jab.</summary>
public partial class App : System.Windows.Application
{
    private AppContainer? _container;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _container = new AppContainer();

        try
        {
            var initializer = _container.GetService<IDatabaseInitializer>();
            await initializer.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Не удалось подготовить схему базы данных.\n\n"
                + "Проверьте, что PostgreSQL запущен (docker compose up -d) и строка подключения "
                + "в appsettings.json / переменной MINIPDM_CONNECTION корректна.\n\n"
                + "Детали: " + ex.Message,
                "Мини-PDM",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        var window = _container.GetService<MainWindow>();
        MainWindow = window;
        window.Show();
    }
}
