namespace MiniPdm.Application.Structure;

/// <summary>Поиск циклических ссылок в дереве состава.</summary>
public static class CycleDetector
{
    private const int Unvisited = 0;
    private const int InProgress = 1;
    private const int Done = 2;

    /// <summary>
    /// Возвращает список циклов. Каждый цикл — последовательность идентификаторов объектов,
    /// начиная с повторно встреченного узла.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Guid>> FindCycles(ProductStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);

        var color = new Dictionary<Guid, int>();
        var stack = new List<Guid>();
        var onStack = new HashSet<Guid>();
        var cycles = new List<IReadOnlyList<Guid>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var nodeId in structure.Nodes.Keys)
        {
            if (!color.TryGetValue(nodeId, out var c) || c == Unvisited)
            {
                Visit(nodeId);
            }
        }

        return cycles;

        void Visit(Guid nodeId)
        {
            color[nodeId] = InProgress;
            stack.Add(nodeId);
            onStack.Add(nodeId);

            foreach (var component in structure.GetComponents(nodeId))
            {
                var child = component.ChildObjectId;
                if (!structure.Nodes.ContainsKey(child))
                {
                    continue;
                }

                if (color.TryGetValue(child, out var childColor) && childColor == InProgress)
                {
                    var start = stack.IndexOf(child);
                    if (start >= 0)
                    {
                        var cycle = stack.Skip(start).ToArray();
                        var key = string.Join('>', cycle.OrderBy(x => x));
                        if (seen.Add(key))
                        {
                            cycles.Add(cycle);
                        }
                    }
                }
                else if (!color.TryGetValue(child, out childColor) || childColor == Unvisited)
                {
                    Visit(child);
                }
            }

            stack.RemoveAt(stack.Count - 1);
            onStack.Remove(nodeId);
            color[nodeId] = Done;
        }
    }

    public static bool HasCycle(ProductStructure structure) => FindCycles(structure).Count > 0;
}
