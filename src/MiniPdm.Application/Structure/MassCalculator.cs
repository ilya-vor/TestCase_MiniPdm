using MiniPdm.Domain;

namespace MiniPdm.Application.Structure;

/// <summary>Компонент, у которого не указана масса.</summary>
public sealed record MissingMassItem(Guid ObjectId, string Name, string? Designation, ObjectType Type);

/// <summary>
/// Результат расчёта массы сборки. <see cref="TotalKg"/> равно <c>null</c>, если массу
/// вычислить нельзя (нет массы у какого-то компонента или в составе есть цикл).
/// </summary>
public sealed record MassResult(
    decimal? TotalKg,
    IReadOnlyList<MissingMassItem> Missing,
    bool HasCycle)
{
    public bool IsComplete => TotalKg.HasValue;
}

/// <summary>Расчёт массы сборки с учётом вложенности и количеств.</summary>
public sealed class MassCalculator
{
    public MassResult Calculate(ProductStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);

        if (CycleDetector.HasCycle(structure))
        {
            return new MassResult(null, Array.Empty<MissingMassItem>(), HasCycle: true);
        }

        var missing = new Dictionary<Guid, MissingMassItem>();
        var cache = new Dictionary<Guid, decimal?>();

        var total = Compute(structure.RootObjectId);

        var ordered = missing.Values
            .OrderBy(m => m.Designation ?? m.Name, StringComparer.CurrentCulture)
            .ToArray();

        return new MassResult(total, ordered, HasCycle: false);

        decimal? Compute(Guid objectId)
        {
            if (cache.TryGetValue(objectId, out var cached))
            {
                return cached;
            }

            var node = structure.Nodes[objectId];
            decimal? result;

            if (node.Type == ObjectType.Assembly)
            {
                decimal sum = 0m;
                var complete = true;

                foreach (var component in structure.GetComponents(objectId))
                {
                    var childMass = Compute(component.ChildObjectId);
                    if (childMass is null)
                    {
                        complete = false;
                        continue;
                    }

                    sum += childMass.Value * component.Quantity;
                }

                result = complete ? sum : null;
            }
            else if (node.MassKg is null)
            {
                missing[objectId] = new MissingMassItem(
                    node.ObjectId, node.Name, node.Designation, node.Type);
                result = null;
            }
            else
            {
                result = node.MassKg;
            }

            cache[objectId] = result;
            return result;
        }
    }
}
