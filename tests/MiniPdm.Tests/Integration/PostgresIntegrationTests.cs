using Dapper;
using MiniPdm.Application;
using MiniPdm.Application.Import;
using MiniPdm.Application.Persistence;
using MiniPdm.Application.Structure;
using MiniPdm.Cad;
using MiniPdm.Domain;
using MiniPdm.Infrastructure;
using MiniPdm.Infrastructure.Database;
using MiniPdm.Infrastructure.Persistence;

namespace MiniPdm.Tests.Integration;

/// <summary>
/// Сквозные тесты на реальном PostgreSQL (Testcontainers). Если Docker недоступен,
/// тесты не выполняются (return), чтобы <c>dotnet test</c> оставался зелёным.
/// </summary>
[Collection("postgres")]
[Trait("Category", "Integration")]
public sealed class PostgresIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public PostgresIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Import_cad_export_persists_objects_and_calculates_mass()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var ctx = await CreateContextAsync();

        var report = await ctx.Import.ImportAsync(DataPath("cad-export"), null, CancellationToken.None);

        Assert.Equal(35, report.AcceptedCount);
        Assert.Equal(10, report.RejectedCount);
        Assert.Equal(1, report.WarningCount);

        var root = Assert.Single(await ctx.Query.SearchAsync("Редуктор цилиндрический", 200, CancellationToken.None));
        var structure = await ctx.Query.GetStructureAsync(root.Id, CancellationToken.None);
        Assert.NotNull(structure);

        var mass = ctx.Mass.Calculate(structure!);
        Assert.NotNull(mass.TotalKg);
        Assert.True(mass.TotalKg > 0m);

        var specification = ctx.Specification.Build(structure!);
        Assert.NotEmpty(specification.Rows);
    }

    [Fact]
    public async Task Missing_mass_is_reported_for_assembly()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var ctx = await CreateContextAsync();
        await ctx.Import.ImportAsync(DataPath("cad-export"), null, CancellationToken.None);

        var root = Assert.Single(await ctx.Query.SearchAsync("Маслоуказатель в сборе", 200, CancellationToken.None));
        var structure = await ctx.Query.GetStructureAsync(root.Id, CancellationToken.None);
        Assert.NotNull(structure);

        var mass = ctx.Mass.Calculate(structure!);
        Assert.Null(mass.TotalKg);
        Assert.Contains(mass.Missing, m => m.Name == "Прокладка маслоуказателя");
    }

    [Fact]
    public async Task Reimport_after_approval_creates_new_versions_for_changed_objects()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var ctx = await CreateContextAsync();
        await ctx.Import.ImportAsync(DataPath("cad-export"), null, CancellationToken.None);

        await using (var connection = await ctx.Factory.OpenAsync(CancellationToken.None))
        {
            await connection.ExecuteAsync("UPDATE object_version SET state = 'Approved';");
        }

        var report = await ctx.Import.ImportAsync(DataPath("cad-export-v2"), null, CancellationToken.None);

        // Изменилась масса — новая версия «В работе».
        var gear = Assert.Single(await ctx.Query.SearchAsync("Колесо зубчатое", 200, CancellationToken.None));
        var gearVersions = await ctx.Query.GetVersionsAsync(gear.Id, CancellationToken.None);
        Assert.Equal(2, gearVersions.Count);
        Assert.Equal(ObjectState.Approved, gearVersions[0].State);
        Assert.Equal(ObjectState.InWork, gearVersions[1].State);
        Assert.Equal(ObjectState.InWork, gear.CurrentState);

        // Изменился состав сборки — новая версия «В работе».
        var cover = Assert.Single(await ctx.Query.SearchAsync("Крышка подшипника в сборе", 200, CancellationToken.None));
        Assert.Equal(2, (await ctx.Query.GetVersionsAsync(cover.Id, CancellationToken.None)).Count);

        // Неизменный объект — версия не создаётся.
        var housing = Assert.Single(await ctx.Query.SearchAsync("Корпус маслоуказателя", 200, CancellationToken.None));
        Assert.Single(await ctx.Query.GetVersionsAsync(housing.Id, CancellationToken.None));

        Assert.Contains(
            report.Entries,
            e => e.File == "Колесо зубчатое.m3d" && e.Reason is not null && e.Reason.Contains("новая версия"));
    }

    [Fact]
    public async Task Change_state_follows_transitions_and_updates_current_version()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var ctx = await CreateContextAsync();
        await ctx.Import.ImportAsync(DataPath("cad-export"), null, CancellationToken.None);

        var gear = Assert.Single(await ctx.Query.SearchAsync("Колесо зубчатое", 200, CancellationToken.None));
        var details = await ctx.Query.GetDetailsAsync(gear.Id, CancellationToken.None);
        var version = details!.CurrentVersion!;
        Assert.Equal(ObjectState.InWork, version.State);

        await ctx.Command.ChangeStateAsync(version.Id, ObjectState.Approved, CancellationToken.None);
        await Assert.ThrowsAnyAsync<DomainException>(() =>
            ctx.Command.ChangeStateAsync(version.Id, ObjectState.InWork, CancellationToken.None));
        await ctx.Command.ChangeStateAsync(version.Id, ObjectState.Cancelled, CancellationToken.None);

        var after = await ctx.Query.GetDetailsAsync(gear.Id, CancellationToken.None);
        Assert.Null(after!.CurrentVersion);
    }

    private async Task<TestContext> CreateContextAsync()
    {
        var factory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var initializer = new PostgresDatabaseInitializer(factory);
        await initializer.InitializeAsync(CancellationToken.None);

        await using (var connection = await factory.OpenAsync(CancellationToken.None))
        {
            await connection.ExecuteAsync(
                "TRUNCATE import_log, bom_link, object_version, pdm_object RESTART IDENTITY CASCADE;");
        }

        var store = new PostgresImportStore(factory, new VersionService(), new SystemClock());
        var source = new JsonCadSource(new JsonCadDocumentReader());
        var import = new ImportService(source, new ImportPlanner(), store);
        var query = new PdmQueryRepository(factory);
        var command = new PdmCommandRepository(factory);

        return new TestContext(
            factory,
            import,
            query,
            command,
            new MassCalculator(),
            new SpecificationBuilder());
    }

    private static string DataPath(string folder)
        => Path.Combine(AppContext.BaseDirectory, "TestData", folder);

    private sealed record TestContext(
        IDbConnectionFactory Factory,
        ImportService Import,
        IPdmQueryRepository Query,
        IPdmCommandRepository Command,
        MassCalculator Mass,
        SpecificationBuilder Specification);
}
