using Wpf.Ui.Controls;
using MiniPdm.Ui.ViewModels;

namespace MiniPdm.Ui;

/// <summary>Главное окно в стиле WinUI (WPF-UI / Fluent). Вся логика — в <see cref="MainViewModel"/>.</summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
