using System.Text.Json;
using System.Text.Json.Serialization;
using MiniPdm.Application.Cad;
using MiniPdm.Domain;

namespace MiniPdm.Cad;

/// <summary>
/// Чтение CAD-документов из JSON-файлов. Единственное место, которое знает о формате JSON
/// и о расширениях <c>.a3d</c>/<c>.m3d</c>.
/// </summary>
public sealed class JsonCadDocumentReader : ICadDocumentReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<CadDocument> ReadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = File.OpenRead(path);
        CadFileDto? dto;
        try
        {
            dto = await JsonSerializer.DeserializeAsync<CadFileDto>(stream, SerializerOptions, ct)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }

        if (dto is null)
        {
            throw new InvalidDataException("Файл не содержит данных.");
        }

        var type = ParseType(dto.Type);

        var components = dto.Components?
            .Select(c => new CadComponent(c.File ?? string.Empty, c.Count))
            .ToArray() ?? Array.Empty<CadComponent>();

        return new CadDocument(
            dto.FileName ?? Path.GetFileName(path),
            dto.FormatVersion,
            type,
            dto.Designation,
            dto.Name ?? string.Empty,
            dto.Properties?.Material,
            dto.Properties?.Mass,
            components);
    }

    private static ObjectType ParseType(string? value) => value switch
    {
        "Assembly" => ObjectType.Assembly,
        "Part" => ObjectType.Part,
        "StandardPart" => ObjectType.StandardPart,
        _ => throw new InvalidDataException($"Неизвестный тип документа: «{value}»."),
    };

    private sealed class CadFileDto
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("designation")]
        public string? Designation { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("properties")]
        public CadPropertiesDto? Properties { get; set; }

        [JsonPropertyName("components")]
        public List<CadComponentDto>? Components { get; set; }
    }

    private sealed class CadPropertiesDto
    {
        [JsonPropertyName("material")]
        public string? Material { get; set; }

        [JsonPropertyName("mass")]
        public decimal? Mass { get; set; }
    }

    private sealed class CadComponentDto
    {
        [JsonPropertyName("file")]
        public string? File { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }
}
