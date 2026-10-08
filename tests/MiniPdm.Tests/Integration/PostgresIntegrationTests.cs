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

    [Fact]
    public async Task Shared_subassembly_edges_are_not_duplicated_in_structure()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var ctx = await CreateContextAsync();

        var root = Guid.NewGuid();
        var subA = Guid.NewGuid();
        var subB = Guid.NewGuid();
        var shared = Guid.NewGuid();
        var leaf = Guid.NewGuid();
        var rootV = Guid.NewGuid();
        var subAV = Guid.NewGuid();
        var subBV = Guid.NewGuid();
        var sharedV = Guid.NewGuid();
        var leafV = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var connection = await ctx.Factory.OpenAsync(CancellationToken.None))
        {
            // Объекты заводятся с current_version_id = NULL, затем проставляется текущая версия:
            // FK на версию неотложный, поэтому порядок важен (как в PostgresImportStore).
            await connection.ExecuteAsync("""
                INSERT INTO pdm_object (id, object_type, designation, name, current_version_id) VALUES
                    (@Root, 'Assembly', 'РДЦЛ.304112.001', 'Корень', NULL),
                    (@SubA, 'Assembly', 'РДЦЛ.304112.002', 'Подузел A', NULL),
                    (@SubB, 'Assembly', 'РДЦЛ.304112.003', 'Подузел B', NULL),
                    (@Shared, 'Assembly', 'РДЦЛ.304112.004', 'Общий подузел', NULL),
                    (@Leaf, 'Part', 'РДЦЛ.304112.005', 'Деталь', NULL);
                """, new { Root = root, SubA = subA, SubB = subB, Shared = shared, Leaf = leaf });

            await connection.ExecuteAsync("""
                INSERT INTO object_version (id, object_id, version_no, state, material, mass_kg, created_at) VALUES
                    (@RootV, @Root, 1, 'InWork', NULL, NULL, @Now),
                    (@SubAV, @SubA, 1, 'InWork', NULL, NULL, @Now),
                    (@SubBV, @SubB, 1, 'InWork', NULL, NULL, @Now),
                    (@SharedV, @Shared, 1, 'InWork', NULL, NULL, @Now),
                    (@LeafV, @Leaf, 1, 'InWork', 'Сталь', 2, @Now);
                """, new
            {
                RootV = rootV,
                SubAV = subAV,
                SubBV = subBV,
                SharedV = sharedV,
                LeafV = leafV,
                Root = root,
                SubA = subA,
                SubB = subB,
                Shared = shared,
                Leaf = leaf,
                Now = now,
            });

            await connection.ExecuteAsync("""
                UPDATE pdm_object SET current_version_id = CASE id
                    WHEN @Root THEN @RootV
                    WHEN @SubA THEN @SubAV
                    WHEN @SubB THEN @SubBV
                    WHEN @Shared THEN @SharedV
                    WHEN @Leaf THEN @LeafV END
                WHERE id IN (@Root, @SubA, @SubB, @Shared, @Leaf);
                """, new
            {
                Root = root,
                SubA = subA,
                SubB = subB,
                Shared = shared,
                Leaf = leaf,
                RootV = rootV,
                SubAV = subAV,
                SubBV = subBV,
                SharedV = sharedV,
                LeafV = leafV,
            });

            await connection.ExecuteAsync("""
                INSERT INTO bom_link (id, parent_version_id, child_object_id, quantity) VALUES
                    (gen_random_uuid(), @RootV, @SubA, 1),
                    (gen_random_uuid(), @RootV, @SubB, 1),
                    (gen_random_uuid(), @SubAV, @Shared, 1),
                    (gen_random_uuid(), @SubBV, @Shared, 1),
                    (gen_random_uuid(), @SharedV, @Leaf, 3);
                """, new
            {
                RootV = rootV,
                SubAV = subAV,
                SubBV = subBV,
                SharedV = sharedV,
                SubA = subA,
                SubB = subB,
                Shared = shared,
                Leaf = leaf,
            });
        }

        var structure = await ctx.Query.GetStructureAsync(root, CancellationToken.None);
        Assert.NotNull(structure);

        // Общий подузел достижим двумя путями; его связь с деталью должна быть одна (иначе задвоение).
        var component = Assert.Single(structure!.GetComponents(shared));
        Assert.Equal(leaf, component.ChildObjectId);
        Assert.Equal(3, component.Quantity);

        // Масса корня = 2 кг × 3 шт × 2 пути = 12 кг (а не 24 при задвоении рёбер).
        var mass = ctx.Mass.Calculate(structure);
        Assert.Equal(12m, mass.TotalKg);
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
