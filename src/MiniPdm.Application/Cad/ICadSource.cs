namespace MiniPdm.Application.Cad;

/// <summary>Результат чтения одного документа: либо документ, либо человекочитаемая ошибка.</summary>
/// <param name="Source">Идентификатор документа (имя файла на диске).</param>
public sealed record CadReadResult(string Source, CadDocument? Document, string? Error)
{
    public bool IsSuccess => Document is not null;

    public static CadReadResult Ok(string source, CadDocument document) => new(source, document, null);

    public static CadReadResult Fail(string source, string error) => new(source, null, error);
}

/// <summary>
/// Пакетное чтение папки с CAD-документами. Именно этот порт подменяется в тестах импорта,
/// чтобы не обращаться к файловой системе.
/// </summary>
public interface ICadSource
{
    Task<IReadOnlyList<CadReadResult>> ReadFolderAsync(string folderPath, CancellationToken ct);
}
