namespace MiniPdm.Application;

/// <summary>Абстракция часов для тестируемости и детерминированности.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
