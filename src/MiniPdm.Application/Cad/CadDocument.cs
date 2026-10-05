using MiniPdm.Domain;

namespace MiniPdm.Application.Cad;

/// <summary>Ссылка сборки на компонент по имени файла в той же папке.</summary>
public sealed record CadComponent(string File, int Count);

/// <summary>
/// Документ CAD-системы в терминах предметной области. Не содержит сведений о формате хранения
/// (JSON) и файловой системе — это забота реализации <see cref="ICadDocumentReader"/>.
/// </summary>
public sealed record CadDocument(
    string FileName,
    int FormatVersion,
    ObjectType Type,
    string? Designation,
    string Name,
    string? Material,
    decimal? MassKg,
    IReadOnlyList<CadComponent> Components);
