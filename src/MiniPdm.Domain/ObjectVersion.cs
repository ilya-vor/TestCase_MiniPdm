namespace MiniPdm.Domain;

/// <summary>Версия объекта с её атрибутами и состоянием.</summary>
public sealed class ObjectVersion
{
    public ObjectVersion(
        Guid id,
        Guid objectId,
        int versionNo,
        ObjectState state,
        string? material,
        decimal? massKg,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор версии не может быть пустым.", nameof(id));
        }

        if (objectId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор объекта не может быть пустым.", nameof(objectId));
        }

        if (versionNo <= 0)
        {
            throw new DomainException("Номер версии должен быть больше нуля.");
        }

        if (massKg is < 0)
        {
            throw new DomainException("Масса не может быть отрицательной.");
        }

        Id = id;
        ObjectId = objectId;
        VersionNo = versionNo;
        State = state;
        Material = string.IsNullOrWhiteSpace(material) ? null : material.Trim();
        MassKg = massKg;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid ObjectId { get; }

    /// <summary>Номер версии: 1, 2, 3…</summary>
    public int VersionNo { get; }

    public ObjectState State { get; private set; }

    /// <summary>Материал (кг за 1 шт. описывает <see cref="MassKg"/>), только для деталей.</summary>
    public string? Material { get; private set; }

    /// <summary>Масса в килограммах за одну штуку. <c>null</c> — масса не задана.</summary>
    public decimal? MassKg { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsEditable => ObjectStateMachine.IsEditable(State);

    public bool ParticipatesInCalculations => ObjectStateMachine.ParticipatesInCalculations(State);

    /// <summary>Меняет состояние версии, проверяя допустимость перехода.</summary>
    public void TransitionTo(ObjectState target)
    {
        ObjectStateMachine.EnsureCanTransition(State, target);
        State = target;
    }

    /// <summary>
    /// Обновляет атрибуты версии. Разрешено только для версии «В работе».
    /// </summary>
    public void UpdateAttributes(string? material, decimal? massKg)
    {
        if (!IsEditable)
        {
            throw new DomainException(
                $"Версию в состоянии «{State}» изменять нельзя — требуется новая версия.");
        }

        if (massKg is < 0)
        {
            throw new DomainException("Масса не может быть отрицательной.");
        }

        Material = string.IsNullOrWhiteSpace(material) ? null : material.Trim();
        MassKg = massKg;
    }
}
