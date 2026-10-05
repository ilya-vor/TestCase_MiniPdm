namespace MiniPdm.Domain;

/// <summary>Базовое исключение нарушения бизнес-правил.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Обозначение не соответствует упрощённому формату ЕСКД.</summary>
public sealed class InvalidDesignationException : DomainException
{
    public InvalidDesignationException(string? value)
        : base($"Обозначение «{value}» не соответствует формату ЕСКД (ЧЧЧЧ.ГГГГГГ.ККК).")
    {
        Value = value;
    }

    public string? Value { get; }
}

/// <summary>Недопустимый переход состояния версии.</summary>
public sealed class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(ObjectState from, ObjectState to)
        : base($"Недопустимый переход состояния: «{from}» → «{to}».")
    {
        From = from;
        To = to;
    }

    public ObjectState From { get; }

    public ObjectState To { get; }
}

/// <summary>В структуре изделия обнаружен цикл.</summary>
public sealed class CyclicStructureException : DomainException
{
    public CyclicStructureException(string message) : base(message)
    {
    }

    public CyclicStructureException(string message, IReadOnlyList<Guid> cyclePath) : base(message)
    {
        CyclePath = cyclePath;
    }

    public IReadOnlyList<Guid> CyclePath { get; } = Array.Empty<Guid>();
}
