using MiniPdm.Domain;

namespace MiniPdm.Application.Persistence;

/// <summary>Изменение данных PDM.</summary>
public interface IPdmCommandRepository
{
    /// <summary>
    /// Меняет состояние версии по правилам переходов и при необходимости пересчитывает
    /// текущую версию объекта.
    /// </summary>
    Task<VersionInfo> ChangeStateAsync(Guid versionId, ObjectState target, CancellationToken ct);
}

/// <summary>Создание схемы БД (миграция/скрипт из репозитория).</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken ct);
}
