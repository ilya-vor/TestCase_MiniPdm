using System.Collections.ObjectModel;
using MiniPdm.Application;
using MiniPdm.Application.Persistence;
using MiniPdm.Ui.Mvvm;

namespace MiniPdm.Ui.ViewModels;

/// <summary>Узел дерева состава с ленивой подгрузкой дочерних элементов.</summary>
public sealed class PdmTreeNodeViewModel : ObservableObject
{
    private readonly IPdmService _service;
    private readonly Action<PdmObjectSummary> _onSelected;
    private bool _childrenLoaded;
    private bool _isExpanded;
    private bool _isSelected;

    public PdmTreeNodeViewModel(
        IPdmService service,
        PdmObjectSummary summary,
        Action<PdmObjectSummary> onSelected)
    {
        _service = service;
        _onSelected = onSelected;
        Summary = summary;
    }

    public PdmObjectSummary Summary { get; }

    public ObservableCollection<PdmTreeNodeViewModel> Children { get; } = new();

    public string Header => Summary.Designation is null
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
            if (SetProperty(ref _isSelected, value) && value)
            {
                _onSelected(Summary);
            }
        }
    }

    private async Task LoadChildrenAsync()
    {
        if (_childrenLoaded)
        {
            return;
        }

        _childrenLoaded = true;
        try
        {
            var children = await _service.GetChildrenAsync(Summary.Id, CancellationToken.None).ConfigureAwait(true);
            foreach (var child in children)
            {
                Children.Add(new PdmTreeNodeViewModel(_service, child, _onSelected));
            }
        }
        catch
        {
            _childrenLoaded = false;
        }
    }
}
