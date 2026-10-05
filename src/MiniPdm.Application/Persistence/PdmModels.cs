using MiniPdm.Domain;

namespace MiniPdm.Application.Persistence;

/// <summary>Краткая информация об объекте для списков и поиска.</summary>
public sealed record PdmObjectSummary(
    Guid Id,
    ObjectType Type,
    string? Designation,
    string Name,
    ObjectState? CurrentState,
    int? CurrentVersionNo);

/// <summary>Информация о версии объекта.</summary>
public sealed record VersionInfo(
    Guid Id,
    Guid ObjectId,
    int VersionNo,
    ObjectState State,
    string? Material,
    decimal? MassKg,
    DateTimeOffset CreatedAt);

/// <summary>Карточка объекта.</summary>
public sealed record PdmObjectDetails(
    PdmObjectSummary Object,
    VersionInfo? CurrentVersion,
    bool IsAssembly,
    int ChildCount);
