using MiniPdm.Application;

namespace MiniPdm.Infrastructure;

/// <summary>Системные часы.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
