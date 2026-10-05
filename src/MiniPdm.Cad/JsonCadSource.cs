using MiniPdm.Application.Cad;

namespace MiniPdm.Cad;

/// <summary>
/// Пакетное чтение папки с CAD-документами. Ошибки отдельных файлов не прерывают чтение,
/// а возвращаются как <see cref="CadReadResult.Fail"/>.
/// </summary>
public sealed class JsonCadSource : ICadSource
{
    private static readonly string[] Extensions = [".a3d", ".m3d"];

    private readonly ICadDocumentReader _reader;

    public JsonCadSource(ICadDocumentReader reader)
        => _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    public async Task<IReadOnlyList<CadReadResult>> ReadFolderAsync(string folderPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Не задан путь к папке.", nameof(folderPath));
        }

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Папка «{folderPath}» не найдена.");
        }

        var files = Directory
            .EnumerateFiles(folderPath)
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.CurrentCulture)
            .ToArray();

        var results = new List<CadReadResult>(files.Length);
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var source = Path.GetFileName(file);
            try
            {
                var document = await _reader.ReadAsync(file, ct).ConfigureAwait(false);
                results.Add(CadReadResult.Ok(source, document));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException or FormatException)
            {
                results.Add(CadReadResult.Fail(source, ex.Message));
            }
        }

        return results;
    }
}
