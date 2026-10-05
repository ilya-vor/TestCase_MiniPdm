using MiniPdm.Application.Cad;
using MiniPdm.Application.Import;
using MiniPdm.Domain;

namespace MiniPdm.Tests.Import;

public sealed class ImportPlannerTests
{
    private readonly ImportPlanner _planner = new();

    [Fact]
    public void Valid_set_is_accepted()
    {
        var plan = _planner.Plan(new[]
        {
            Part("Деталь.m3d", "РДЦЛ.304112.301", "Деталь", 1m),
            Std("Болт.m3d", "Болт М12", 0.05m),
            Assembly("Сборка.a3d", "РДЦЛ.304112.300", "Сборка", ("Деталь.m3d", 2), ("Болт.m3d", 4)),
        });

        Assert.Equal(3, plan.Accepted.Count);
        Assert.Equal(3, plan.Entries.Count(e => e.Outcome == ImportOutcome.Accepted));
    }

    [Fact]
    public void Broken_file_is_rejected_and_does_not_stop_import()
    {
        var plan = _planner.Plan(new[]
        {
            CadReadResult.Fail("Отдушина.m3d", "Unexpected end of JSON"),
            Part("Деталь.m3d", "РДЦЛ.304112.301", "Деталь", 1m),
        });

        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "Отдушина.m3d").Outcome);
        Assert.Contains("Не удалось прочитать", Entry(plan, "Отдушина.m3d").Reason);
        Assert.Equal(ImportOutcome.Accepted, Entry(plan, "Деталь.m3d").Outcome);
        Assert.Single(plan.Accepted);
    }

    [Fact]
    public void Duplicate_designation_rejects_all_participants()
    {
        var plan = _planner.Plan(new[]
        {
            Part("Шайба стопорная.m3d", "РДЦЛ.304112.710", "Шайба стопорная", 0.02m),
            Part("Шайба упорная.m3d", "РДЦЛ.304112.710", "Шайба упорная", 0.05m),
        });

        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "Шайба стопорная.m3d").Outcome);
        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "Шайба упорная.m3d").Outcome);
        Assert.Empty(plan.Accepted);
    }

    [Fact]
    public void Duplicate_standard_part_name_rejects_all_participants()
    {
        var plan = _planner.Plan(new[]
        {
            Std("Болт1.m3d", "Болт М12", 0.05m),
            Std("Болт2.m3d", "Болт М12", 0.06m),
        });

        Assert.All(plan.Entries, e => Assert.Equal(ImportOutcome.Rejected, e.Outcome));
    }

    [Fact]
    public void Invalid_designation_is_rejected()
    {
        var plan = _planner.Plan(new[]
        {
            Part("Втулка.m3d", "PДЦЛ.304112.601", "Втулка", 0.42m),
            Part("Кольцо.m3d", "РДЦЛ.30411.602", "Кольцо", 0.18m),
        });

        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "Втулка.m3d").Outcome);
        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "Кольцо.m3d").Outcome);
    }

    [Fact]
    public void Part_without_mass_is_imported_with_warning()
    {
        var plan = _planner.Plan(new[] { Part("Прокладка.m3d", "РДЦЛ.304112.902", "Прокладка", null) });

        var entry = Entry(plan, "Прокладка.m3d");
        Assert.Equal(ImportOutcome.Warning, entry.Outcome);
        Assert.Single(plan.Accepted);
    }

    [Fact]
    public void Part_without_material_is_rejected()
    {
        var plan = _planner.Plan(new[] { Part("Деталь.m3d", "РДЦЛ.304112.301", "Деталь", 1m, material: null) });

        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "Деталь.m3d").Outcome);
    }

    [Fact]
    public void Component_with_non_positive_count_rejects_assembly()
    {
        var plan = _planner.Plan(new[]
        {
            Part("Деталь.m3d", "РДЦЛ.304112.301", "Деталь", 1m),
            Assembly("Сборка.a3d", "РДЦЛ.304112.300", "Сборка", ("Деталь.m3d", 0)),
        });

        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "Сборка.a3d").Outcome);
        Assert.Contains("больше нуля", Entry(plan, "Сборка.a3d").Reason);
        Assert.Equal(ImportOutcome.Accepted, Entry(plan, "Деталь.m3d").Outcome);
    }

    [Fact]
    public void Missing_reference_rejects_assembly()
    {
        var plan = _planner.Plan(new[]
        {
            Assembly("Привод.a3d", "РДЦЛ.304112.300", "Привод", ("Насос НШ-10.m3d", 1)),
        });

        var entry = Entry(plan, "Привод.a3d");
        Assert.Equal(ImportOutcome.Rejected, entry.Outcome);
        Assert.Contains("отсутствующий", entry.Reason);
    }

    [Fact]
    public void Rejection_propagates_up_the_tree()
    {
        var plan = _planner.Plan(new[]
        {
            // Привод отклонён из-за отсутствующей детали, значит Редуктор тоже отклонён.
            Assembly("Привод.a3d", "РДЦЛ.304112.310", "Привод", ("Нет.m3d", 1)),
            Assembly("Редуктор.a3d", "РДЦЛ.304112.300", "Редуктор", ("Привод.a3d", 1)),
        });

        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "Редуктор.a3d").Outcome);
        Assert.Contains("отклонён", Entry(plan, "Редуктор.a3d").Reason);
    }

    [Fact]
    public void Cycle_is_rejected()
    {
        var plan = _planner.Plan(new[]
        {
            Assembly("A.a3d", "РДЦЛ.304112.301", "A", ("B.a3d", 1)),
            Assembly("B.a3d", "РДЦЛ.304112.302", "B", ("A.a3d", 1)),
        });

        Assert.All(plan.Entries, e => Assert.Equal(ImportOutcome.Rejected, e.Outcome));
        Assert.Empty(plan.Accepted);
    }

    [Fact]
    public void Self_reference_is_rejected_as_cycle()
    {
        var plan = _planner.Plan(new[]
        {
            Assembly("A.a3d", "РДЦЛ.304112.301", "A", ("A.a3d", 1)),
        });

        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "A.a3d").Outcome);
    }

    [Fact]
    public void Standard_part_with_designation_is_rejected()
    {
        var plan = _planner.Plan(new[]
        {
            CadReadResult.Ok("Болт.m3d", new CadDocument(
                "Болт.m3d", 1, ObjectType.StandardPart, "РДЦЛ.304112.001", "Болт М12", null, 0.05m, Array.Empty<CadComponent>())),
        });

        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "Болт.m3d").Outcome);
    }

    [Fact]
    public void Unsupported_format_version_is_rejected()
    {
        var plan = _planner.Plan(new[]
        {
            CadReadResult.Ok("Деталь.m3d", new CadDocument(
                "Деталь.m3d", 2, ObjectType.Part, "РДЦЛ.304112.301", "Деталь", "Сталь", 1m, Array.Empty<CadComponent>())),
        });

        Assert.Equal(ImportOutcome.Rejected, Entry(plan, "Деталь.m3d").Outcome);
    }

    private static ImportReportEntry Entry(ImportPlan plan, string file)
        => plan.Entries.Single(e => e.File == file);

    private static CadReadResult Part(string file, string designation, string name, decimal? mass, string? material = "Сталь")
        => CadReadResult.Ok(file, new CadDocument(file, 1, ObjectType.Part, designation, name, material, mass, Array.Empty<CadComponent>()));

    private static CadReadResult Std(string file, string name, decimal? mass)
        => CadReadResult.Ok(file, new CadDocument(file, 1, ObjectType.StandardPart, null, name, null, mass, Array.Empty<CadComponent>()));

    private static CadReadResult Assembly(string file, string designation, string name, params (string File, int Count)[] components)
        => CadReadResult.Ok(file, new CadDocument(
            file,
            1,
            ObjectType.Assembly,
            designation,
            name,
            null,
            null,
            components.Select(c => new CadComponent(c.File, c.Count)).ToArray()));
}
