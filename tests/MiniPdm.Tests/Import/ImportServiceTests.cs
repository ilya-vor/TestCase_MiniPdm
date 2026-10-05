using MiniPdm.Application.Cad;
using MiniPdm.Application.Import;
using MiniPdm.Domain;

namespace MiniPdm.Tests.Import;

public sealed class ImportServiceTests
{
    [Fact]
    public async Task Import_merges_apply_outcomes_into_report()
    {
        var source = new FakeCadSource(new[]
        {
            CadReadResult.Ok("Деталь.m3d", new CadDocument(
                "Деталь.m3d", 1, ObjectType.Part, "РДЦЛ.304112.301", "Деталь", "Сталь", 1m, Array.Empty<CadComponent>())),
            CadReadResult.Fail("Битый.m3d", "ошибка"),
        });

        var store = new FakeImportStore(plan => plan.Accepted
            .Select(c => new ObjectApplyOutcome(c.Source, VersionAction.CreateObject, 1))
            .ToArray());

        var service = new ImportService(source, new ImportPlanner(), store);
        var report = await service.ImportAsync("ignored", null, CancellationToken.None);

        Assert.Equal(1, report.AcceptedCount);
        Assert.Equal(1, report.RejectedCount);
        Assert.Contains("Создан объект", report.Entries.Single(e => e.File == "Деталь.m3d").Reason);
    }

    [Fact]
    public async Task Import_service_does_not_touch_file_system_when_source_is_fake()
    {
        // Источник подменён и путь не существует — имитация теста без файловой системы.
        var source = new FakeCadSource(Array.Empty<CadReadResult>());
        var store = new FakeImportStore(_ => Array.Empty<ObjectApplyOutcome>());
        var service = new ImportService(source, new ImportPlanner(), store);

        var report = await service.ImportAsync("/definitely/not/a/real/path", null, CancellationToken.None);

        Assert.Empty(report.Entries);
        Assert.Equal("/definitely/not/a/real/path", source.RequestedFolder);
    }

    private sealed class FakeCadSource : ICadSource
    {
        private readonly IReadOnlyList<CadReadResult> _results;

        public FakeCadSource(IReadOnlyList<CadReadResult> results) => _results = results;

        public string? RequestedFolder { get; private set; }

        public Task<IReadOnlyList<CadReadResult>> ReadFolderAsync(string folderPath, CancellationToken ct)
        {
            RequestedFolder = folderPath;
            return Task.FromResult(_results);
        }
    }

    private sealed class FakeImportStore : IImportStore
    {
        private readonly Func<ImportPlan, IReadOnlyList<ObjectApplyOutcome>> _handler;

        public FakeImportStore(Func<ImportPlan, IReadOnlyList<ObjectApplyOutcome>> handler) => _handler = handler;

        public Task<IReadOnlyList<ObjectApplyOutcome>> ApplyAsync(ImportPlan plan, CancellationToken ct)
            => Task.FromResult(_handler(plan));
    }
}
