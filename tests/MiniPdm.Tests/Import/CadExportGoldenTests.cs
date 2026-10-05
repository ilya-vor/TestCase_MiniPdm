using MiniPdm.Application.Import;
using MiniPdm.Cad;

namespace MiniPdm.Tests.Import;

/// <summary>
/// Проверка на реальном наборе cad-export/cad-export-v2: обнаружение всех заложенных ошибок
/// и совпадение количества принятых/отклонённых/предупреждённых файлов.
/// </summary>
public sealed class CadExportGoldenTests
{
    private static readonly string[] ExpectedRejected =
    [
        "Втулка распорная.m3d",
        "Кольцо распорное.m3d",
        "Механизм переключения.a3d",
        "Отдушина.m3d",
        "Привод масляного насоса.a3d",
        "Пробка сливная в сборе.a3d",
        "Рычаг переключения.a3d",
        "Тяга переключения.a3d",
        "Шайба стопорная.m3d",
        "Шайба упорная.m3d",
    ];

    public static TheoryData<string> Folders => new() { "cad-export", "cad-export-v2" };

    [Theory]
    [MemberData(nameof(Folders))]
    public async Task Report_matches_expected(string folder)
    {
        var source = new JsonCadSource(new JsonCadDocumentReader());
        var results = await source.ReadFolderAsync(DataPath(folder), CancellationToken.None);

        Assert.Equal(45, results.Count);

        var plan = new ImportPlanner().Plan(results);

        var rejected = plan.Entries
            .Where(e => e.Outcome == ImportOutcome.Rejected)
            .Select(e => e.File)
            .OrderBy(f => f, StringComparer.CurrentCulture)
            .ToArray();

        Assert.Equal(ExpectedRejected.OrderBy(f => f, StringComparer.CurrentCulture), rejected);

        var warning = Assert.Single(plan.Entries.Where(e => e.Outcome == ImportOutcome.Warning));
        Assert.Equal("Прокладка маслоуказателя.m3d", warning.File);

        Assert.Equal(35, plan.Accepted.Count);
        Assert.Equal(10, plan.Entries.Count(e => e.Outcome == ImportOutcome.Rejected));
    }

    private static string DataPath(string folder)
        => Path.Combine(AppContext.BaseDirectory, "TestData", folder);
}
