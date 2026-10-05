using Dapper;
using MiniPdm.Application;
using MiniPdm.Application.Import;
using MiniPdm.Domain;
using MiniPdm.Infrastructure.Database;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>
/// Запись импорта в PostgreSQL. Все изменения выполняются в одной транзакции.
/// Решение о версии принимает <see cref="VersionService"/>.
/// </summary>
public sealed class PostgresImportStore : IImportStore
{
    private readonly IDbConnectionFactory _factory;
    private readonly VersionService _versionService;
    private readonly IClock _clock;

    public PostgresImportStore(IDbConnectionFactory factory, VersionService versionService, IClock clock)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<IReadOnlyList<ObjectApplyOutcome>> ApplyAsync(ImportPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);

        await using var connection = await _factory.OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        var existingRows = (await connection.QueryAsync<ExistingObjectRow>(
            new CommandDefinition(ExistingObjectsSql, transaction: transaction, cancellationToken: ct))).ToArray();

        var byIdentity = new Dictionary<string, ExistingObjectRow>(StringComparer.Ordinal);
        foreach (var row in existingRows)
        {
            byIdentity[IdentityKey(row.ObjectType, row.Designation, row.Name)] = row;
        }

        var bomRows = (await connection.QueryAsync<BomRow>(
            new CommandDefinition(CurrentBomSql, transaction: transaction, cancellationToken: ct))).ToArray();

        var existingBom = bomRows
            .GroupBy(r => r.ParentVersionId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<BomSignature>)g
                    .Select(x => new BomSignature(x.ChildObjectId, x.Quantity))
                    .ToHashSet());

        // 1. Сопоставляем файлы импорта с объектами и заводим отсутствующие объекты.
        var objectIdBySource = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<ResolvedCandidate>(plan.Accepted.Count);

        foreach (var candidate in plan.Accepted)
        {
            var key = IdentityKey(candidate);
            var exists = byIdentity.TryGetValue(key, out var existing);
            var objectId = exists ? existing!.Id : Guid.NewGuid();
            objectIdBySource[candidate.Source] = objectId;
            resolved.Add(new ResolvedCandidate(candidate, objectId, exists ? existing : null));
        }

        const string insertObjectSql = """
            INSERT INTO pdm_object (id, object_type, designation, name, current_version_id)
            VALUES (@Id, @Type, @Designation, @Name, NULL);
            """;
        foreach (var item in resolved.Where(r => r.Existing is null))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertObjectSql,
                new
                {
                    Id = item.ObjectId,
                    Type = item.Candidate.Type.ToDb(),
                    Designation = item.Candidate.Designation?.Value,
                    Name = item.Candidate.Name,
                },
                transaction,
                cancellationToken: ct));
        }

        // 2. Сигнатуры состава новых данных для сравнения с текущими версиями.
        var candidateBom = new Dictionary<string, IReadOnlySet<BomSignature>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in resolved)
        {
            var set = new HashSet<BomSignature>();
            if (item.Candidate.Type == ObjectType.Assembly)
            {
                foreach (var component in item.Candidate.Components)
                {
                    if (objectIdBySource.TryGetValue(component.File, out var childId))
                    {
                        set.Add(new BomSignature(childId, component.Count));
                    }
                }
            }

            candidateBom[item.Candidate.Source] = set;
        }

        // 3. Применяем правила версий и пишем версии со связями.
        var outcomes = new List<ObjectApplyOutcome>(resolved.Count);
        var now = _clock.UtcNow;

        foreach (var item in resolved)
        {
            ExistingVersionSnapshot? snapshot = null;
            if (item.Existing is { CurrentVersionId: not null, State: not null } existing)
            {
                var components = existingBom.TryGetValue(existing.CurrentVersionId.Value, out var set)
                    ? set
                    : new HashSet<BomSignature>();
                snapshot = new ExistingVersionSnapshot(
                    PdmMapping.ToObjectState(existing.State),
                    existing.Material,
                    existing.MassKg,
                    components);
            }

            var decision = _versionService.Decide(
                item.Existing is not null,
                snapshot,
                item.Candidate,
                candidateBom[item.Candidate.Source]);

            switch (decision.Action)
            {
                case VersionAction.CreateObject:
                {
                    var versionId = Guid.NewGuid();
                    await InsertVersionAsync(connection, transaction, versionId, item, 1, now, ct);
                    await SetCurrentVersionAsync(connection, transaction, item.ObjectId, versionId, ct);
                    await InsertLinksAsync(connection, transaction, versionId, item, objectIdBySource, ct);
                    outcomes.Add(new ObjectApplyOutcome(item.Candidate.Source, VersionAction.CreateObject, 1));
                    break;
                }

                case VersionAction.UpdateInWork:
                {
                    var versionId = item.Existing!.CurrentVersionId!.Value;
                    const string updateSql = """
                        UPDATE object_version
                        SET material = @Material, mass_kg = @MassKg
                        WHERE id = @VersionId;
                        """;
                    await connection.ExecuteAsync(new CommandDefinition(
                        updateSql,
                        new { Material = item.Candidate.Material, MassKg = item.Candidate.MassKg, VersionId = versionId },
                        transaction,
                        cancellationToken: ct));

                    await DeleteLinksAsync(connection, transaction, versionId, ct);
                    await InsertLinksAsync(connection, transaction, versionId, item, objectIdBySource, ct);
                    outcomes.Add(new ObjectApplyOutcome(
                        item.Candidate.Source, VersionAction.UpdateInWork, item.Existing.VersionNo ?? 0));
                    break;
                }

                case VersionAction.CreateNewVersion:
                {
                    var versionNo = (item.Existing?.MaxVersionNo ?? 0) + 1;
                    var versionId = Guid.NewGuid();
                    await InsertVersionAsync(connection, transaction, versionId, item, versionNo, now, ct);
                    await SetCurrentVersionAsync(connection, transaction, item.ObjectId, versionId, ct);
                    await InsertLinksAsync(connection, transaction, versionId, item, objectIdBySource, ct);
                    outcomes.Add(new ObjectApplyOutcome(
                        item.Candidate.Source, VersionAction.CreateNewVersion, versionNo));
                    break;
                }

                case VersionAction.NoChange:
                default:
                {
                    outcomes.Add(new ObjectApplyOutcome(
                        item.Candidate.Source, VersionAction.NoChange, item.Existing?.VersionNo ?? 0));
                    break;
                }
            }
        }

        // 4. Журнал импорта.
        const string logSql = """
            INSERT INTO import_log (started_at, file_name, severity, reason)
            VALUES (@StartedAt, @FileName, @Severity, @Reason);
            """;
        foreach (var entry in plan.Entries)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                logSql,
                new
                {
                    StartedAt = now,
                    FileName = entry.File,
                    Severity = entry.Outcome.ToString(),
                    Reason = entry.Reason,
                },
                transaction,
                cancellationToken: ct));
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return outcomes;
    }

    private static async Task InsertVersionAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        Guid versionId,
        ResolvedCandidate item,
        int versionNo,
        DateTimeOffset now,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO object_version (id, object_id, version_no, state, material, mass_kg, created_at)
            VALUES (@Id, @ObjectId, @VersionNo, @State, @Material, @MassKg, @CreatedAt);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = versionId,
                ObjectId = item.ObjectId,
                VersionNo = versionNo,
                State = ObjectState.InWork.ToDb(),
                Material = item.Candidate.Material,
                MassKg = item.Candidate.MassKg,
                CreatedAt = now,
            },
            transaction,
            cancellationToken: ct));
    }

    private static async Task SetCurrentVersionAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        Guid objectId,
        Guid versionId,
        CancellationToken ct)
    {
        const string sql = "UPDATE pdm_object SET current_version_id = @VersionId WHERE id = @ObjectId;";
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { VersionId = versionId, ObjectId = objectId }, transaction, cancellationToken: ct));
    }

    private static async Task DeleteLinksAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        Guid versionId,
        CancellationToken ct)
    {
        const string sql = "DELETE FROM bom_link WHERE parent_version_id = @VersionId;";
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { VersionId = versionId }, transaction, cancellationToken: ct));
    }

    private static async Task InsertLinksAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        Guid versionId,
        ResolvedCandidate item,
        IReadOnlyDictionary<string, Guid> objectIdBySource,
        CancellationToken ct)
    {
        if (item.Candidate.Type != ObjectType.Assembly)
        {
            return;
        }

        const string sql = """
            INSERT INTO bom_link (id, parent_version_id, child_object_id, quantity)
            VALUES (@Id, @ParentVersionId, @ChildObjectId, @Quantity);
            """;

        var links = item.Candidate.Components
            .Where(c => objectIdBySource.ContainsKey(c.File))
            .GroupBy(c => objectIdBySource[c.File])
            .Select(g => new { ChildObjectId = g.Key, Quantity = g.Sum(x => x.Count) });

        foreach (var link in links)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    Id = Guid.NewGuid(),
                    ParentVersionId = versionId,
                    ChildObjectId = link.ChildObjectId,
                    Quantity = link.Quantity,
                },
                transaction,
                cancellationToken: ct));
        }
    }

    private static string IdentityKey(string objectType, string? designation, string name)
        => objectType == "StandardPart"
            ? "N:" + name
            : "D:" + (designation ?? string.Empty);

    private static string IdentityKey(ImportCandidate candidate)
        => candidate.Type == ObjectType.StandardPart
            ? "N:" + candidate.Name
            : "D:" + candidate.Designation!.Value;

    private const string ExistingObjectsSql = """
        SELECT o.id                 AS Id,
               o.object_type        AS ObjectType,
               o.designation        AS Designation,
               o.name               AS Name,
               o.current_version_id AS CurrentVersionId,
               v.state              AS State,
               v.material           AS Material,
               v.mass_kg            AS MassKg,
               v.version_no         AS VersionNo,
               COALESCE(m.max_version, 0) AS MaxVersionNo
        FROM pdm_object o
        LEFT JOIN object_version v ON v.id = o.current_version_id
        LEFT JOIN (
            SELECT object_id, MAX(version_no) AS max_version
            FROM object_version
            GROUP BY object_id
        ) m ON m.object_id = o.id;
        """;

    private const string CurrentBomSql = """
        SELECT l.parent_version_id AS ParentVersionId,
               l.child_object_id   AS ChildObjectId,
               l.quantity          AS Quantity
        FROM bom_link l
        JOIN pdm_object o ON o.current_version_id = l.parent_version_id;
        """;

    private sealed record ResolvedCandidate(ImportCandidate Candidate, Guid ObjectId, ExistingObjectRow? Existing);

    public sealed class ExistingObjectRow
    {
        public Guid Id { get; set; }

        public string ObjectType { get; set; } = string.Empty;

        public string? Designation { get; set; }

        public string Name { get; set; } = string.Empty;

        public Guid? CurrentVersionId { get; set; }

        public string? State { get; set; }

        public string? Material { get; set; }

        public decimal? MassKg { get; set; }

        public int? VersionNo { get; set; }

        public int MaxVersionNo { get; set; }
    }

    public sealed class BomRow
    {
        public Guid ParentVersionId { get; set; }

        public Guid ChildObjectId { get; set; }

        public int Quantity { get; set; }
    }
}
