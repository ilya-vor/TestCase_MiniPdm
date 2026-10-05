namespace MiniPdm.Application.Cad;

/// <summary>
/// Абстракция над CAD-системой. Остальной код не знает о JSON и файловой системе.
/// Реализация выбрасывает исключение, если документ невозможно прочитать.
/// </summary>
public interface ICadDocumentReader
{
    Task<CadDocument> ReadAsync(string path, CancellationToken ct);
}
