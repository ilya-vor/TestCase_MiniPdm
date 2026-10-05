using MiniPdm.Application.Structure;

namespace MiniPdm.Application;

/// <summary>Экспорт сводной спецификации.</summary>
public interface ISpecificationExporter
{
    Task ExportCsvAsync(string filePath, IReadOnlyList<SpecificationRow> rows, CancellationToken ct);
}
