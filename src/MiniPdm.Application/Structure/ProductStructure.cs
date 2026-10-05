using MiniPdm.Domain;

namespace MiniPdm.Application.Structure;

/// <summary>Узел структуры изделия — текущая версия объекта.</summary>
public sealed record StructureNode(
    Guid ObjectId,
    Guid VersionId,
    ObjectType Type,
    string? Designation,
    string Name,
    ObjectState State,
    string? Material,
    decimal? MassKg);

/// <summary>Связь состава: дочерний объект и количество.</summary>
public sealed record StructureComponent(Guid ChildObjectId, int Quantity);

/// <summary>
/// Снимок дерева состава, построенный по результату рекурсивного запроса.
/// Содержит только текущие версии объектов.
/// </summary>
public sealed class ProductStructure
{
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<StructureComponent>> _components;

    public ProductStructure(
        Guid rootObjectId,
        IReadOnlyDictionary<Guid, StructureNode> nodes,
        IReadOnlyDictionary<Guid, IReadOnlyList<StructureComponent>> components)
    {
        RootObjectId = rootObjectId;
        Nodes = nodes;
        _components = components;
    }

    public Guid RootObjectId { get; }

    public IReadOnlyDictionary<Guid, StructureNode> Nodes { get; }

    public StructureNode Root =>
        Nodes.TryGetValue(RootObjectId, out var root)
            ? root
            : throw new DomainException("Корневой объект отсутствует в структуре.");

    public bool IsAssembly => Root.Type == ObjectType.Assembly;

    public IReadOnlyList<StructureComponent> GetComponents(Guid objectId)
        => _components.TryGetValue(objectId, out var list) ? list : Array.Empty<StructureComponent>();
}
