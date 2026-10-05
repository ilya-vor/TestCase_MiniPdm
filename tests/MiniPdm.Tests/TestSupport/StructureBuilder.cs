using MiniPdm.Application.Structure;
using MiniPdm.Domain;

namespace MiniPdm.Tests.TestSupport;

/// <summary>Удобный конструктор дерева состава для тестов.</summary>
public sealed class StructureBuilder
{
    private readonly List<StructureNode> _nodes = new();
    private readonly Dictionary<Guid, List<StructureComponent>> _components = new();

    public Guid RootId { get; private set; }

    public StructureBuilder Add(
        Guid id,
        string name,
        ObjectType type,
        decimal? mass = null,
        string? designation = null,
        ObjectState state = ObjectState.InWork)
    {
        _nodes.Add(new StructureNode(id, Guid.NewGuid(), type, designation, name, state, null, mass));
        if (RootId == Guid.Empty)
        {
            RootId = id;
        }

        return this;
    }

    public StructureBuilder Root(Guid id)
    {
        RootId = id;
        return this;
    }

    public StructureBuilder Link(Guid parent, Guid child, int quantity)
    {
        if (!_components.TryGetValue(parent, out var list))
        {
            list = new List<StructureComponent>();
            _components[parent] = list;
        }

        list.Add(new StructureComponent(child, quantity));
        return this;
    }

    public ProductStructure Build()
    {
        var nodes = _nodes.ToDictionary(n => n.ObjectId);
        var components = _components.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<StructureComponent>)kvp.Value);
        return new ProductStructure(RootId, nodes, components);
    }
}
