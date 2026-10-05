using MiniPdm.Domain;

namespace MiniPdm.Application.Structure;

/// <summary>Строка сводной спецификации.</summary>
public sealed record SpecificationRow(
    string? Designation,
    string Name,
    ObjectType Type,
    int TotalQuantity,
    decimal? UnitMassKg,
    decimal? TotalMassKg);

/// <summary>Результат построения сводной спецификации.</summary>
public sealed record SpecificationResult(IReadOnlyList<SpecificationRow> Rows, bool HasCycle);

/// <summary>
/// Сводная спецификация сборки: плоский список всех деталей и стандартных изделий с суммарным
/// количеством по всему дереву. Количества перемножаются по пути и складываются по вхождениям.
/// </summary>
public sealed class SpecificationBuilder
{
    public SpecificationResult Build(ProductStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);

        if (CycleDetector.HasCycle(structure))
        {
            return new SpecificationResult(Array.Empty<SpecificationRow>(), HasCycle: true);
        }

        var totals = new Dictionary<Guid, int>();
        var path = new HashSet<Guid>();

        Walk(structure.RootObjectId, 1);

        var rows = new List<SpecificationRow>();
        foreach (var (objectId, quantity) in totals)
        {
            var node = structure.Nodes[objectId];
            rows.Add(new SpecificationRow(
                node.Designation,
                node.Name,
                node.Type,
                quantity,
                node.MassKg,
                node.MassKg is null ? null : node.MassKg.Value * quantity));
        }

        rows.Sort(static (a, b) =>
        {
            var byDesignation = string.Compare(a.Designation ?? a.Name, b.Designation ?? b.Name, StringComparison.CurrentCulture);
            return byDesignation != 0 ? byDesignation : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
        });

        return new SpecificationResult(rows, HasCycle: false);

        void Walk(Guid objectId, int multiplier)
        {
            if (!path.Add(objectId))
            {
                return;
            }

            var node = structure.Nodes[objectId];

            if (node.Type == ObjectType.Assembly)
            {
                foreach (var component in structure.GetComponents(objectId))
                {
                    Walk(component.ChildObjectId, multiplier * component.Quantity);
                }
            }
            else
            {
                totals[objectId] = totals.TryGetValue(objectId, out var current)
                    ? current + multiplier
                    : multiplier;
            }

            path.Remove(objectId);
        }
    }
}
