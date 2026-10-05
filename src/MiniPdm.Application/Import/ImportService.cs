using MiniPdm.Application.Cad;

namespace MiniPdm.Application.Import;

/// <summary>
/// Оркестратор импорта папки: читает документы через <see cref="ICadSource"/>,
/// планирует импорт и применяет результат в БД.
/// </summary>
public sealed class ImportService
{
    private readonly ICadSource _source;
    private readonly ImportPlanner _planner;
    private readonly IImportStore _store;

    public ImportService(ICadSource source, ImportPlanner planner, IImportStore store)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ImportReport> ImportAsync(
        string folderPath,
        IProgress<ImportProgress>? progress,
        CancellationToken ct)
    {
        progress?.Report(new ImportProgress("Чтение папки", 0, 1));
        var results = await _source.ReadFolderAsync(folderPath, ct).ConfigureAwait(false);

        progress?.Report(new ImportProgress("Анализ данных", 0, results.Count));
        var plan = _planner.Plan(results);

        progress?.Report(new ImportProgress("Сохранение", 0, plan.Accepted.Count));
        var outcomes = await _store.ApplyAsync(plan, ct).ConfigureAwait(false);
        var byFile = outcomes.ToDictionary(o => o.File, StringComparer.OrdinalIgnoreCase);

        var entries = new List<ImportReportEntry>(plan.Entries.Count);
        foreach (var entry in plan.Entries)
        {
            if (entry.Outcome == ImportOutcome.Rejected || !byFile.TryGetValue(entry.File, out var outcome))
            {
                entries.Add(entry);
                continue;
            }

            var action = outcome.Action switch
            {
                VersionAction.CreateObject => $"Создан объект (версия {outcome.VersionNo}).",
                VersionAction.UpdateInWork => $"Обновлена версия {outcome.VersionNo} «В работе».",
                VersionAction.CreateNewVersion => $"Создана новая версия {outcome.VersionNo} «В работе».",
                VersionAction.NoChange => "Данные не изменились.",
                _ => entry.Reason,
            };

            var reason = entry.Outcome == ImportOutcome.Warning
                ? $"{entry.Reason} {action}"
                : action;

            entries.Add(new ImportReportEntry(entry.File, entry.Outcome, reason, entry.Type));
        }

        progress?.Report(new ImportProgress("Готово", plan.Accepted.Count, plan.Accepted.Count));
        return new ImportReport(entries);
    }
}
