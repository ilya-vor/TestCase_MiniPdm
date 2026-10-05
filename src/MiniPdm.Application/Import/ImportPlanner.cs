using MiniPdm.Application.Cad;
using MiniPdm.Domain;

namespace MiniPdm.Application.Import;

/// <summary>
/// Планирование импорта: чистая логика без базы данных и файловой системы.
/// Выполняет валидацию, поиск дублей, проверку ссылок с распространением отказов вверх
/// по дереву и обнаружение циклов.
/// </summary>
public sealed class ImportPlanner
{
    private static readonly StringComparer FileComparer = StringComparer.OrdinalIgnoreCase;

    public ImportPlan Plan(IReadOnlyList<CadReadResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var candidates = new Dictionary<string, ImportCandidate>(FileComparer);
        var rejected = new Dictionary<string, string>(FileComparer);
        var warnings = new HashSet<string>(FileComparer);
        var typeBySource = new Dictionary<string, ObjectType>(FileComparer);
        var order = new List<string>();

        var duplicateSources = results
            .GroupBy(r => r.Source, FileComparer)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(FileComparer);

        // 1. Индивидуальная валидация файлов.
        foreach (var result in results)
        {
            var source = result.Source;
            if (!order.Contains(source, FileComparer))
            {
                order.Add(source);
            }

            if (duplicateSources.Contains(source))
            {
                rejected[source] = "Файл с таким именем указан более одного раза.";
                continue;
            }

            if (result.Document is null)
            {
                rejected[source] = $"Не удалось прочитать файл: {result.Error ?? "неизвестная ошибка"}";
                continue;
            }

            var doc = result.Document;
            typeBySource[source] = doc.Type;

            if (doc.FormatVersion != 1)
            {
                rejected[source] = $"Неподдерживаемая версия формата: {doc.FormatVersion}.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(doc.Name))
            {
                rejected[source] = "Не указано наименование.";
                continue;
            }

            Designation? designation = null;
            if (doc.Type == ObjectType.StandardPart)
            {
                if (!string.IsNullOrWhiteSpace(doc.Designation))
                {
                    rejected[source] = "Стандартное изделие не должно иметь обозначение.";
                    continue;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(doc.Designation))
                {
                    rejected[source] = "Не указано обозначение.";
                    continue;
                }

                if (!Designation.TryCreate(doc.Designation, out designation))
                {
                    rejected[source] = $"Неверный формат обозначения «{doc.Designation}».";
                    continue;
                }
            }

            if (doc.Type == ObjectType.Part && string.IsNullOrWhiteSpace(doc.Material))
            {
                rejected[source] = "Не указан материал детали.";
                continue;
            }

            if (doc.Type != ObjectType.Assembly && doc.Components.Count > 0)
            {
                rejected[source] = "Компоненты допустимы только у сборки.";
                continue;
            }

            if (doc.Type != ObjectType.Assembly && doc.MassKg is null)
            {
                warnings.Add(source);
            }

            candidates[source] = new ImportCandidate(
                source,
                doc.Type,
                designation,
                doc.Name.Trim(),
                string.IsNullOrWhiteSpace(doc.Material) ? null : doc.Material.Trim(),
                doc.Type == ObjectType.Assembly ? null : doc.MassKg,
                doc.Type == ObjectType.Assembly ? doc.Components : Array.Empty<CadComponent>());
        }

        // 2. Дубли обозначений и наименований стандартных изделий — отклоняются все участники.
        foreach (var group in candidates.Values
                     .Where(c => c.Type != ObjectType.StandardPart)
                     .GroupBy(c => c.Designation!.Value, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
        {
            foreach (var candidate in group.ToArray())
            {
                rejected[candidate.Source] = $"Дублируется обозначение «{group.Key}».";
                candidates.Remove(candidate.Source);
            }
        }

        foreach (var group in candidates.Values
                     .Where(c => c.Type == ObjectType.StandardPart)
                     .GroupBy(c => c.Name, StringComparer.CurrentCulture)
                     .Where(g => g.Count() > 1))
        {
            foreach (var candidate in group.ToArray())
            {
                rejected[candidate.Source] = $"Дублируется наименование стандартного изделия «{group.Key}».";
                candidates.Remove(candidate.Source);
            }
        }

        // 3. Проверка ссылок и циклов с распространением отказов вверх до неподвижной точки.
        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var assembly in candidates.Values.Where(c => c.Type == ObjectType.Assembly).ToArray())
            {
                foreach (var component in assembly.Components)
                {
                    if (component.Count <= 0)
                    {
                        rejected[assembly.Source] = $"Количество компонента «{component.File}» должно быть больше нуля.";
                        candidates.Remove(assembly.Source);
                        changed = true;
                        break;
                    }

                    if (!candidates.ContainsKey(component.File))
                    {
                        rejected[assembly.Source] = rejected.TryGetValue(component.File, out var childReason)
                            ? $"Компонент «{component.File}» отклонён: {childReason}"
                            : $"Ссылка на отсутствующий файл «{component.File}».";
                        candidates.Remove(assembly.Source);
                        changed = true;
                        break;
                    }
                }
            }

            foreach (var source in FindCycleSources(candidates))
            {
                rejected[source] = "Циклическая ссылка в составе.";
                candidates.Remove(source);
                changed = true;
            }
        }

        // 4. Формирование отчёта.
        var entries = new List<ImportReportEntry>(order.Count);
        foreach (var source in order.OrderBy(s => s, StringComparer.CurrentCulture))
        {
            ObjectType? type = typeBySource.TryGetValue(source, out var value) ? value : null;

            if (rejected.TryGetValue(source, out var reason))
            {
                entries.Add(new ImportReportEntry(source, ImportOutcome.Rejected, reason, type));
            }
            else if (warnings.Contains(source))
            {
                entries.Add(new ImportReportEntry(source, ImportOutcome.Warning, "Не указана масса.", type));
            }
            else
            {
                entries.Add(new ImportReportEntry(source, ImportOutcome.Accepted, null, type));
            }
        }

        var accepted = candidates.Values
            .OrderBy(c => c.Source, StringComparer.CurrentCulture)
            .ToArray();

        return new ImportPlan(accepted, entries);
    }

    private static IReadOnlyList<string> FindCycleSources(IReadOnlyDictionary<string, ImportCandidate> candidates)
    {
        var color = new Dictionary<string, int>(FileComparer);
        var stack = new List<string>();
        var inCycle = new HashSet<string>(FileComparer);

        foreach (var source in candidates.Keys)
        {
            if (!color.ContainsKey(source))
            {
                Visit(source);
            }
        }

        return inCycle.ToArray();

        void Visit(string source)
        {
            color[source] = 1;
            stack.Add(source);

            if (candidates.TryGetValue(source, out var candidate) && candidate.Type == ObjectType.Assembly)
            {
                foreach (var component in candidate.Components)
                {
                    if (!candidates.ContainsKey(component.File))
                    {
                        continue;
                    }

                    if (!color.TryGetValue(component.File, out var childColor))
                    {
                        Visit(component.File);
                    }
                    else if (childColor == 1)
                    {
                        var start = stack.FindIndex(s => FileComparer.Equals(s, component.File));
                        if (start >= 0)
                        {
                            for (var i = start; i < stack.Count; i++)
                            {
                                inCycle.Add(stack[i]);
                            }
                        }
                    }
                }
            }

            stack.RemoveAt(stack.Count - 1);
            color[source] = 2;
        }
    }
}
