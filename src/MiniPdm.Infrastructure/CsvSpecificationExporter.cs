using System.Globalization;
using System.Text;
using MiniPdm.Application;
using MiniPdm.Application.Structure;

namespace MiniPdm.Infrastructure;

/// <summary>Экспорт спецификации в CSV с разделителем «;» (совместим с русским Excel).</summary>
public sealed class CsvSpecificationExporter : ISpecificationExporter
{
    private static readonly string[] Header =
        ["Обозначение", "Наименование", "Тип", "Количество", "Масса за шт., кг", "Масса итого, кг"];

    public async Task ExportCsvAsync(string filePath, IReadOnlyList<SpecificationRow> rows, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', Header));

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            builder.AppendLine(string.Join(
                ';',
                Escape(row.Designation),
                Escape(row.Name),
                Escape(Translate(row)),
                row.TotalQuantity.ToString(CultureInfo.InvariantCulture),
                row.UnitMassKg?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.TotalMassKg?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
        }

        await File.WriteAllTextAsync(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct)
            .ConfigureAwait(false);
    }

    private static string Translate(SpecificationRow row) => row.Type switch
    {
        Domain.ObjectType.Assembly => "Сборка",
        Domain.ObjectType.Part => "Деталь",
        Domain.ObjectType.StandardPart => "Стандартное изделие",
        _ => row.Type.ToString(),
    };

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }
}
