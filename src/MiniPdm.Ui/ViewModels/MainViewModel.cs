using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using MiniPdm.Application;
using MiniPdm.Application.Import;
using MiniPdm.Application.Persistence;
using MiniPdm.Application.Structure;
using MiniPdm.Domain;
using MiniPdm.Ui.Mvvm;

namespace MiniPdm.Ui.ViewModels;

/// <summary>Модель представления главного окна.</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly IPdmService _service;
    private readonly ISpecificationExporter _exporter;

    private string _searchText = string.Empty;
    private PdmObjectSummary? _selectedSearchResult;
    private PdmObjectDetails? _details;
    private string _statusText = "Готово.";
    private string _massText = string.Empty;
    private string _reportSummary = string.Empty;
    private bool _isImporting;
    private double _importProgress;
    private string _importProgressText = string.Empty;
    private CancellationTokenSource? _importCts;

    public MainViewModel(IPdmService service, ISpecificationExporter exporter)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));

        SearchCommand = new AsyncRelayCommand(SearchAsync);
        ApproveCommand = new AsyncRelayCommand(
            () => ChangeStateAsync(ObjectState.Approved),
            () => Details?.CurrentVersion?.State == ObjectState.InWork);
        CancelStateCommand = new AsyncRelayCommand(
            () => ChangeStateAsync(ObjectState.Cancelled),
            () => Details?.CurrentVersion is not null && Details.CurrentVersion.State != ObjectState.Cancelled);
        CalculateMassCommand = new AsyncRelayCommand(CalculateMassAsync, () => Details?.CurrentVersion is not null);
        ExportSpecificationCommand = new AsyncRelayCommand(ExportSpecificationAsync, () => SpecificationRows.Count > 0);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => !IsImporting);
        CancelImportCommand = new RelayCommand(() => _importCts?.Cancel(), () => IsImporting);
        ClearReportCommand = new RelayCommand(ClearReport, () => ImportEntries.Count > 0);
    }

    public ObservableCollection<PdmObjectSummary> SearchResults { get; } = new();

    public ObservableCollection<VersionInfo> Versions { get; } = new();

    public ObservableCollection<PdmTreeNodeViewModel> TreeRoots { get; } = new();

    public ObservableCollection<SpecificationRow> SpecificationRows { get; } = new();

    public ObservableCollection<ImportReportEntry> ImportEntries { get; } = new();

    public ICommand SearchCommand { get; }

    public ICommand ApproveCommand { get; }

    public ICommand CancelStateCommand { get; }

    public ICommand CalculateMassCommand { get; }

    public ICommand ExportSpecificationCommand { get; }

    public ICommand ImportCommand { get; }

    public ICommand CancelImportCommand { get; }

    public ICommand ClearReportCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public PdmObjectSummary? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set
        {
            if (SetProperty(ref _selectedSearchResult, value) && value is not null)
            {
                _ = SelectObjectAsync(value, rebuildTree: true);
            }
        }
    }

    public PdmObjectDetails? Details
    {
        get => _details;
        private set
        {
            if (SetProperty(ref _details, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                RefreshCommands();
            }
        }
    }

    public bool HasSelection => Details is not null;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string MassText
    {
        get => _massText;
        private set => SetProperty(ref _massText, value);
    }

    public string ReportSummary
    {
        get => _reportSummary;
        private set => SetProperty(ref _reportSummary, value);
    }

    public bool IsImporting
    {
        get => _isImporting;
        private set
        {
            if (SetProperty(ref _isImporting, value))
            {
                RefreshCommands();
            }
        }
    }

    public double ImportProgress
    {
        get => _importProgress;
        private set => SetProperty(ref _importProgress, value);
    }

    public string ImportProgressText
    {
        get => _importProgressText;
        private set => SetProperty(ref _importProgressText, value);
    }

    /// <summary>Первичная загрузка: поиск с пустым запросом, чтобы список объектов был виден сразу после запуска.</summary>
    public Task InitializeAsync() => SearchAsync();

    private async Task SearchAsync()
    {
        try
        {
            var results = await _service.SearchAsync(SearchText, CancellationToken.None).ConfigureAwait(true);
            SearchResults.Clear();
            foreach (var item in results)
            {
                SearchResults.Add(item);
            }

            StatusText = $"Найдено объектов: {results.Count}.";
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка поиска: " + ex.Message;
        }
    }

    private async Task SelectObjectAsync(PdmObjectSummary summary, bool rebuildTree)
    {
        try
        {
            Details = await _service.GetDetailsAsync(summary.Id, CancellationToken.None).ConfigureAwait(true);

            Versions.Clear();
            foreach (var version in await _service.GetVersionsAsync(summary.Id, CancellationToken.None).ConfigureAwait(true))
            {
                Versions.Add(version);
            }

            if (rebuildTree)
            {
                TreeRoots.Clear();
                var root = new PdmTreeNodeViewModel(
                    _service,
                    summary,
                    node => _ = SelectObjectAsync(node, rebuildTree: false));
                TreeRoots.Add(root);
                // Разворачиваем корень сразу — состав изделия виден без лишнего клика
                // (дети подгружаются лениво по факту раскрытия).
                root.IsExpanded = true;
            }

            MassText = string.Empty;

            var selectionText = summary.Designation is null
                ? summary.Name
                : $"{summary.Designation} — {summary.Name}";

            try
            {
                // Авто-построение сводной спецификации сразу при выборе объекта (п.4.6 ТЗ).
                StatusText = $"{selectionText}. {await FillSpecificationAsync().ConfigureAwait(true)}";
            }
            catch (Exception ex)
            {
                StatusText = $"{selectionText}. Ошибка построения спецификации: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка загрузки объекта: " + ex.Message;
        }
    }

    private async Task ChangeStateAsync(ObjectState target)
    {
        if (Details?.CurrentVersion is null)
        {
            return;
        }

        var objectSummary = Details.Object;
        try
        {
            await _service.ChangeStateAsync(Details.CurrentVersion.Id, target, CancellationToken.None).ConfigureAwait(true);
            await SelectObjectAsync(objectSummary, rebuildTree: false).ConfigureAwait(true);
            StatusText = "Состояние изменено.";
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка смены состояния: " + ex.Message;
        }
    }

    private async Task CalculateMassAsync()
    {
        if (Details is null)
        {
            return;
        }

        try
        {
            var result = await _service.CalculateMassAsync(Details.Object.Id, CancellationToken.None).ConfigureAwait(true);
            MassText = result.HasCycle
                ? "В составе обнаружен цикл — масса не определена."
                : result.TotalKg is not null
                    ? $"Масса изделия: {result.TotalKg.Value:0.###} кг"
                    : "Масса не определена. Не указана масса у: " + string.Join(
                        "; ",
                        result.Missing.Select(m => m.Designation is null ? m.Name : $"{m.Designation} {m.Name}"));
        }
        catch (Exception ex)
        {
            MassText = "Ошибка расчёта массы: " + ex.Message;
        }
    }

    /// <summary>
    /// Строит сводную спецификацию для выбранного объекта, заполняет коллекцию и возвращает
    /// текст статуса. Вызывается автоматически при выборе объекта.
    /// </summary>
    private async Task<string> FillSpecificationAsync()
    {
        var result = await _service
            .BuildSpecificationAsync(Details!.Object.Id, CancellationToken.None)
            .ConfigureAwait(true);

        SpecificationRows.Clear();
        foreach (var row in result.Rows)
        {
            SpecificationRows.Add(row);
        }

        RefreshCommands();

        return result.HasCycle
            ? "В составе обнаружен цикл — спецификация недоступна."
            : $"Строк спецификации: {result.Rows.Count}.";
    }

    private async Task ExportSpecificationAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = "specification.csv",
            Title = "Экспорт сводной спецификации",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _exporter.ExportCsvAsync(dialog.FileName, SpecificationRows.ToList(), CancellationToken.None)
                .ConfigureAwait(true);
            StatusText = "Спецификация экспортирована: " + dialog.FileName;
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка экспорта: " + ex.Message;
        }
    }

    private async Task ImportAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Выберите папку с CAD-документами" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _importCts = new CancellationTokenSource();
        IsImporting = true;
        ImportProgress = 0;
        ImportProgressText = "Чтение папки";

        var progress = new Progress<ImportProgress>(p =>
        {
            ImportProgressText = p.Stage;
            ImportProgress = p.Total > 0 ? Math.Min(100d, p.Processed * 100d / p.Total) : 0d;
        });

        try
        {
            var report = await _service
                .ImportFolderAsync(dialog.FolderName, progress, _importCts.Token)
                .ConfigureAwait(true);

            ImportEntries.Clear();
            foreach (var entry in report.Entries)
            {
                ImportEntries.Add(entry);
            }

            ReportSummary =
                $"Принято: {report.AcceptedCount}, отклонено: {report.RejectedCount}, предупреждений: {report.WarningCount}.";
            ImportProgress = 100;
            ImportProgressText = "Готово";
            StatusText = "Импорт завершён.";

            await SearchAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Импорт отменён.";
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка импорта: " + ex.Message;
        }
        finally
        {
            IsImporting = false;
            _importCts.Dispose();
            _importCts = null;
            RefreshCommands();
        }
    }

    private void ClearReport()
    {
        ImportEntries.Clear();
        ReportSummary = string.Empty;
        RefreshCommands();
    }

    private static void RefreshCommands() => CommandManager.InvalidateRequerySuggested();
}
