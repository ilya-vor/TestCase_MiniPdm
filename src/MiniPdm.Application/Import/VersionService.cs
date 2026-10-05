using MiniPdm.Domain;

namespace MiniPdm.Application.Import;

/// <summary>Действие, которое нужно применить к объекту при импорте.</summary>
public enum VersionAction
{
    /// <summary>Объекта ещё нет — создать объект и первую версию.</summary>
    CreateObject = 0,

    /// <summary>Текущая версия «В работе» и данные изменились — обновить её.</summary>
    UpdateInWork = 1,

    /// <summary>Текущая версия «Утверждено» и данные изменились — создать новую версию «В работе».</summary>
    CreateNewVersion = 2,

    /// <summary>Данные не изменились — ничего не делать.</summary>
    NoChange = 3,
}

/// <summary>Сигнатура состава для сравнения (дочерний объект + количество).</summary>
public sealed record BomSignature(Guid ChildObjectId, int Quantity);

/// <summary>Снимок текущей версии существующего объекта.</summary>
public sealed record ExistingVersionSnapshot(
    ObjectState State,
    string? Material,
    decimal? MassKg,
    IReadOnlySet<BomSignature> Components);

/// <summary>Решение по версии.</summary>
public sealed record VersionDecision(VersionAction Action, string Reason);

/// <summary>
/// Правила версий и состояний при повторном импорте:
/// данные не изменились — ничего; текущая «В работе» — обновление; «Утверждено» — новая версия.
/// </summary>
public sealed class VersionService
{
    public VersionDecision Decide(
        bool objectExists,
        ExistingVersionSnapshot? current,
        ImportCandidate candidate,
        IReadOnlySet<BomSignature> candidateComponents)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candidateComponents);

        if (!objectExists)
        {
            return new VersionDecision(VersionAction.CreateObject, "Объект создан.");
        }

        if (current is null)
        {
            return new VersionDecision(VersionAction.CreateNewVersion, "Создана новая версия «В работе».");
        }

        var unchanged =
            string.Equals(Normalize(current.Material), Normalize(candidate.Material), StringComparison.Ordinal)
            && current.MassKg == candidate.MassKg
            && current.Components.SetEquals(candidateComponents);

        if (unchanged)
        {
            return new VersionDecision(VersionAction.NoChange, "Данные не изменились.");
        }

        return current.State == ObjectState.InWork
            ? new VersionDecision(VersionAction.UpdateInWork, "Обновлена текущая версия «В работе».")
            : new VersionDecision(VersionAction.CreateNewVersion, "Создана новая версия «В работе».");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
