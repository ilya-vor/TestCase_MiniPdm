using MiniPdm.Application.Cad;
using MiniPdm.Domain;

namespace MiniPdm.Application.Import;

/// <summary>Результат по файлу в отчёте импорта.</summary>
public enum ImportOutcome
{
    /// <summary>Объект принят без замечаний.</summary>
    Accepted = 0,

    /// <summary>Объект принят с предупреждением (например, не указана масса).</summary>
    Warning = 1,

    /// <summary>Файл отклонён.</summary>
    Rejected = 2,
}

/// <summary>Строка отчёта импорта.</summary>
public sealed record ImportReportEntry(string File, ImportOutcome Outcome, string? Reason, ObjectType? Type = null);

/// <summary>Итоговый отчёт импорта.</summary>
public sealed record ImportReport(IReadOnlyList<ImportReportEntry> Entries)
{
    /// <summary>Принято объектов (включая принятые с предупреждением).</summary>
    public int AcceptedCount => Entries.Count(e => e.Outcome != ImportOutcome.Rejected);

    public int RejectedCount => Entries.Count(e => e.Outcome == ImportOutcome.Rejected);

    public int WarningCount => Entries.Count(e => e.Outcome == ImportOutcome.Warning);
}

/// <summary>Проверенный и принятый к импорту объект с ссылками на другие файлы.</summary>
public sealed record ImportCandidate(
    string Source,
    ObjectType Type,
    Designation? Designation,
    string Name,
    string? Material,
    decimal? MassKg,
    IReadOnlyList<CadComponent> Components);

/// <summary>Результат планирования импорта: что принять и полный отчёт.</summary>
public sealed record ImportPlan(
    IReadOnlyList<ImportCandidate> Accepted,
    IReadOnlyList<ImportReportEntry> Entries);

/// <summary>Прогресс импорта для индикатора в интерфейсе.</summary>
public sealed record ImportProgress(string Stage, int Processed, int Total);
