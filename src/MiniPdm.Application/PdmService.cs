using MiniPdm.Application.Import;
using MiniPdm.Application.Persistence;
using MiniPdm.Application.Structure;
using MiniPdm.Domain;

namespace MiniPdm.Application;

/// <summary>
/// Фасад приложения: единая точка входа для интерфейса. Скрывает репозитории и расчётные сервисы.
/// </summary>
public interface IPdmService
{
    Task<IReadOnlyList<PdmObjectSummary>> SearchAsync(string? term, CancellationToken ct);

    Task<PdmObjectDetails?> GetDetailsAsync(Guid objectId, CancellationToken ct);

    Task<IReadOnlyList<VersionInfo>> GetVersionsAsync(Guid objectId, CancellationToken ct);

    Task<ProductStructure?> GetStructureAsync(Guid objectId, CancellationToken ct);

    Task<IReadOnlyList<PdmObjectSummary>> GetChildrenAsync(Guid objectId, CancellationToken ct);

    Task<MassResult> CalculateMassAsync(Guid objectId, CancellationToken ct);

    Task<SpecificationResult> BuildSpecificationAsync(Guid objectId, CancellationToken ct);

    Task<VersionInfo> ChangeStateAsync(Guid versionId, ObjectState target, CancellationToken ct);

    Task<ImportReport> ImportFolderAsync(
        string folderPath,
        IProgress<ImportProgress>? progress,
        CancellationToken ct);
}

/// <inheritdoc />
public sealed class PdmService : IPdmService
{
    private readonly IPdmQueryRepository _query;
    private readonly IPdmCommandRepository _command;
    private readonly MassCalculator _massCalculator;
    private readonly SpecificationBuilder _specificationBuilder;
    private readonly ImportService _importService;

    public PdmService(
        IPdmQueryRepository query,
        IPdmCommandRepository command,
        MassCalculator massCalculator,
        SpecificationBuilder specificationBuilder,
        ImportService importService)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _massCalculator = massCalculator ?? throw new ArgumentNullException(nameof(massCalculator));
        _specificationBuilder = specificationBuilder ?? throw new ArgumentNullException(nameof(specificationBuilder));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
    }

    public Task<IReadOnlyList<PdmObjectSummary>> SearchAsync(string? term, CancellationToken ct)
        => _query.SearchAsync(term, 200, ct);

    public Task<PdmObjectDetails?> GetDetailsAsync(Guid objectId, CancellationToken ct)
        => _query.GetDetailsAsync(objectId, ct);

    public Task<IReadOnlyList<VersionInfo>> GetVersionsAsync(Guid objectId, CancellationToken ct)
        => _query.GetVersionsAsync(objectId, ct);

    public Task<ProductStructure?> GetStructureAsync(Guid objectId, CancellationToken ct)
        => _query.GetStructureAsync(objectId, ct);

    public Task<IReadOnlyList<PdmObjectSummary>> GetChildrenAsync(Guid objectId, CancellationToken ct)
        => _query.GetChildrenAsync(objectId, ct);

    public async Task<MassResult> CalculateMassAsync(Guid objectId, CancellationToken ct)
    {
        var structure = await _query.GetStructureAsync(objectId, ct).ConfigureAwait(false)
            ?? throw new DomainException("Объект не найден.");
        return _massCalculator.Calculate(structure);
    }

    public async Task<SpecificationResult> BuildSpecificationAsync(Guid objectId, CancellationToken ct)
    {
        var structure = await _query.GetStructureAsync(objectId, ct).ConfigureAwait(false)
            ?? throw new DomainException("Объект не найден.");
        return _specificationBuilder.Build(structure);
    }

    public Task<VersionInfo> ChangeStateAsync(Guid versionId, ObjectState target, CancellationToken ct)
        => _command.ChangeStateAsync(versionId, target, ct);

    public Task<ImportReport> ImportFolderAsync(
        string folderPath,
        IProgress<ImportProgress>? progress,
        CancellationToken ct)
        => _importService.ImportAsync(folderPath, progress, ct);
}
