using System.Collections.ObjectModel;
using MiniPdm.Application;
using MiniPdm.Application.Persistence;
using MiniPdm.Domain;
using MiniPdm.Ui.Mvvm;

namespace MiniPdm.Ui.ViewModels;

/// <summary>Узел дерева состава с ленивой подгрузкой дочерних элементов.</summary>
public sealed class PdmTreeNodeViewModel : ObservableObject
{
    private readonly IPdmService? _service;
    private readonly Action<PdmObjectSummary>? _onSelected;
    private readonly bool _isPlaceholder;
    private bool _childrenLoaded;
    private bool _isExpanded;
    private bool _isSelected;

    public PdmTreeNodeViewModel(
        IPdmService service,
        PdmObjectSummary summary,
        Action<PdmObjectSummary> onSelected)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));

        // WPF показывает стрелку раскрытия только у элементов, у которых есть дети.
        // Дети грузятся лениво, поэтому до первого раскрытия их ещё нет и стрелка
        // была бы скрыта. Для сборок кладём узел-заглушку, чтобы стрелка была видна сразу.
        if (summary.Type == ObjectType.Assembly)
        {
            Children.Add(new PdmTreeNodeViewModel());
        }
    }

    /// <summary>Узел-заглушка: существует только до первой загрузки дочерних элементов.</summary>
    private PdmTreeNodeViewModel()
    {
        _isPlaceholder = true;
    }

    public PdmObjectSummary? Summary { get; }

    public ObservableCollection<PdmTreeNodeViewModel> Children { get; } = new();

    public string Header => _isPlaceholder || Summary is null
        ? "…"
        : Summary.Designation is null
            ? Summary.Name
            : $"{Summary.Designation} — {Summary.Name}";

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value)
            {
                _ = LoadChildrenAsync();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value && Summary is not null)
            {
                _onSelected?.Invoke(Summary);
            }
        }
    }

    private async Task LoadChildrenAsync()
    {
        if (_childrenLoaded || _isPlaceholder || _service is null || Summary is null)
        {
            return;
        }

        _childrenLoaded = true;
        try
        {
            var children = await _service.GetChildrenAsync(Summary.Id, CancellationToken.None).ConfigureAwait(true);

            Children.Clear(); // убираем узел-заглушку
            foreach (var child in children)
            {
                Children.Add(new PdmTreeNodeViewModel(_service, child, _onSelected!));
            }
        }
        catch
        {
            _childrenLoaded = false;
        }
    }
}
