using System.Windows;
using MiniPdm.Ui.ViewModels;

namespace MiniPdm.Ui;

/// <summary>Главное окно. Вся логика — в <see cref="MainViewModel"/>.</summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
