namespace MiniPdm.Application.Import;

/// <summary>Результат применения импорта к одному объекту.</summary>
public sealed record ObjectApplyOutcome(string File, VersionAction Action, int VersionNo);

/// <summary>
/// Запись принятых объектов в БД одной транзакцией. Реализация сама решает,
/// какой версии коснуться, применяя <see cref="VersionService"/>.
/// </summary>
public interface IImportStore
{
    Task<IReadOnlyList<ObjectApplyOutcome>> ApplyAsync(ImportPlan plan, CancellationToken ct);
}
