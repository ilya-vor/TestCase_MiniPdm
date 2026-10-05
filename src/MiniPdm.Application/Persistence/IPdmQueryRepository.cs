using MiniPdm.Application.Structure;

namespace MiniPdm.Application.Persistence;

/// <summary>Чтение данных PDM.</summary>
public interface IPdmQueryRepository
{
    /// <summary>Поиск по обозначению или наименованию. Пустой запрос возвращает первые <paramref name="limit"/> объектов.</summary>
    Task<IReadOnlyList<PdmObjectSummary>> SearchAsync(string? term, int limit, CancellationToken ct);

    Task<PdmObjectDetails?> GetDetailsAsync(Guid objectId, CancellationToken ct);

    Task<IReadOnlyList<VersionInfo>> GetVersionsAsync(Guid objectId, CancellationToken ct);

    /// <summary>Дерево состава выбирается одним рекурсивным CTE.</summary>
    Task<ProductStructure?> GetStructureAsync(Guid objectId, CancellationToken ct);

    /// <summary>Прямые потомки версии (для ленивой подгрузки дерева).</summary>
    Task<IReadOnlyList<PdmObjectSummary>> GetChildrenAsync(Guid parentObjectId, CancellationToken ct);
}
