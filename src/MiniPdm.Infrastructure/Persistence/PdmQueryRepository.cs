using Dapper;
using MiniPdm.Application.Persistence;
using MiniPdm.Application.Structure;
using MiniPdm.Domain;
using MiniPdm.Infrastructure.Database;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>Чтение данных PDM из PostgreSQL.</summary>
public sealed class PdmQueryRepository : IPdmQueryRepository
{
    private readonly IDbConnectionFactory _factory;

    public PdmQueryRepository(IDbConnectionFactory factory)
        => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public async Task<IReadOnlyList<PdmObjectSummary>> SearchAsync(string? term, int limit, CancellationToken ct)
    {
        const string sql = """
            SELECT o.id           AS Id,
                   o.object_type  AS ObjectType,
                   o.designation  AS Designation,
                   o.name         AS Name,
                   v.state        AS State,
                   v.version_no   AS VersionNo
            FROM pdm_object o
            LEFT JOIN object_version v ON v.id = o.current_version_id
            WHERE (@Term IS NULL
                   OR o.name ILIKE '%' || @Term || '%'
                   OR o.designation ILIKE '%' || @Term || '%')
            ORDER BY COALESCE(o.designation, o.name)
            LIMIT @Limit;
            """;

        var normalized = string.IsNullOrWhiteSpace(term) ? null : term.Trim();

        await using var connection = await _factory.OpenAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ObjectRow>(
            new CommandDefinition(sql, new { Term = normalized, Limit = limit }, cancellationToken: ct));

        return rows.Select(MapSummary).ToArray();
    }

    public async Task<PdmObjectDetails?> GetDetailsAsync(Guid objectId, CancellationToken ct)
    {
        const string sql = """
            SELECT o.id           AS Id,
                   o.object_type  AS ObjectType,
                   o.designation  AS Designation,
                   o.name         AS Name,
                   v.id           AS VersionId,
                   v.version_no   AS VersionNo,
                   v.state        AS State,
                   v.material     AS Material,
                   v.mass_kg      AS MassKg,
                   v.created_at   AS CreatedAt,
                   (SELECT COUNT(*) FROM bom_link l WHERE l.parent_version_id = o.current_version_id) AS ChildCount
            FROM pdm_object o
            LEFT JOIN object_version v ON v.id = o.current_version_id
            WHERE o.id = @Id;
            """;

        await using var connection = await _factory.OpenAsync(ct).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<DetailsRow>(
            new CommandDefinition(sql, new { Id = objectId }, cancellationToken: ct));

        if (row is null)
        {
            return null;
        }

        var summary = MapSummary(row);
        VersionInfo? version = row.VersionId is null
            ? null
            : new VersionInfo(
                row.VersionId.Value,
                row.Id,
                row.VersionNo ?? 0,
                PdmMapping.ToObjectState(row.State!),
                row.Material,
                row.MassKg,
                row.CreatedAt ?? default);

        var type = PdmMapping.ToObjectType(row.ObjectType);
        return new PdmObjectDetails(summary, version, type == ObjectType.Assembly, row.ChildCount);
    }

    public async Task<IReadOnlyList<VersionInfo>> GetVersionsAsync(Guid objectId, CancellationToken ct)
    {
        const string sql = """
            SELECT id         AS Id,
                   object_id  AS ObjectId,
                   version_no AS VersionNo,
                   state      AS State,
                   material   AS Material,
                   mass_kg    AS MassKg,
                   created_at AS CreatedAt
            FROM object_version
            WHERE object_id = @ObjectId
            ORDER BY version_no;
            """;

        await using var connection = await _factory.OpenAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<VersionRow>(
            new CommandDefinition(sql, new { ObjectId = objectId }, cancellationToken: ct));

        return rows
            .Select(r => new VersionInfo(
                r.Id, r.ObjectId, r.VersionNo, PdmMapping.ToObjectState(r.State), r.Material, r.MassKg, r.CreatedAt))
            .ToArray();
    }

    public async Task<IReadOnlyList<PdmObjectSummary>> GetChildrenAsync(Guid parentObjectId, CancellationToken ct)
    {
        const string sql = """
            SELECT c.id          AS Id,
                   c.object_type AS ObjectType,
                   c.designation AS Designation,
                   c.name        AS Name,
                   cv.state      AS State,
                   cv.version_no AS VersionNo
            FROM pdm_object p
            JOIN bom_link l ON l.parent_version_id = p.current_version_id
            JOIN pdm_object c ON c.id = l.child_object_id
            LEFT JOIN object_version cv ON cv.id = c.current_version_id
            WHERE p.id = @ParentId
            ORDER BY COALESCE(c.designation, c.name);
            """;

        await using var connection = await _factory.OpenAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ObjectRow>(
            new CommandDefinition(sql, new { ParentId = parentObjectId }, cancellationToken: ct));

        return rows.Select(MapSummary).ToArray();
    }

    public async Task<ProductStructure?> GetStructureAsync(Guid objectId, CancellationToken ct)
    {
        const string sql = """
            WITH RECURSIVE tree AS (
                SELECT o.id           AS object_id,
                       v.id           AS version_id,
                       o.object_type  AS object_type,
                       o.designation  AS designation,
                       o.name         AS name,
                       v.state        AS state,
                       v.material     AS material,
                       v.mass_kg      AS mass_kg,
                       NULL::uuid     AS parent_version_id,
                       NULL::integer  AS quantity,
                       0              AS depth,
                       ARRAY[o.id]    AS path
                FROM pdm_object o
                JOIN object_version v ON v.id = o.current_version_id
                WHERE o.id = @RootId
                UNION ALL
                SELECT c.id, cv.id, c.object_type, c.designation, c.name,
                       cv.state, cv.material, cv.mass_kg,
                       t.version_id, l.quantity, t.depth + 1, t.path || c.id
                FROM tree t
                JOIN bom_link l ON l.parent_version_id = t.version_id
                JOIN pdm_object c ON c.id = l.child_object_id
                JOIN object_version cv ON cv.id = c.current_version_id
                WHERE NOT (c.id = ANY(t.path))
            )
            SELECT object_id        AS ObjectId,
                   version_id       AS VersionId,
                   object_type      AS ObjectType,
                   designation      AS Designation,
                   name             AS Name,
                   state            AS State,
                   material         AS Material,
                   mass_kg          AS MassKg,
                   parent_version_id AS ParentVersionId,
                   quantity         AS Quantity,
                   depth            AS Depth
            FROM tree;
            """;

        await using var connection = await _factory.OpenAsync(ct).ConfigureAwait(false);
        var rows = (await connection.QueryAsync<BomTreeRow>(
            new CommandDefinition(sql, new { RootId = objectId }, cancellationToken: ct))).ToArray();

        if (rows.Length == 0)
        {
            return null;
        }

        var nodes = new Dictionary<Guid, StructureNode>();
        var versionToObject = new Dictionary<Guid, Guid>();
        foreach (var row in rows)
        {
            versionToObject[row.VersionId] = row.ObjectId;
            nodes[row.ObjectId] = new StructureNode(
                row.ObjectId,
                row.VersionId,
                PdmMapping.ToObjectType(row.ObjectType),
                row.Designation,
                row.Name,
                PdmMapping.ToObjectState(row.State),
                row.Material,
                row.MassKg);
        }

        var components = new Dictionary<Guid, List<StructureComponent>>();
        foreach (var row in rows.Where(r => r.ParentVersionId is not null && r.Quantity is not null))
        {
            if (!versionToObject.TryGetValue(row.ParentVersionId!.Value, out var parentObjectId))
            {
                continue;
            }

            if (!components.TryGetValue(parentObjectId, out var list))
            {
                list = new List<StructureComponent>();
                components[parentObjectId] = list;
            }

            list.Add(new StructureComponent(row.ObjectId, row.Quantity!.Value));
        }

        var componentsReadOnly = components.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<StructureComponent>)kvp.Value);

        return new ProductStructure(objectId, nodes, componentsReadOnly);
    }

    private static PdmObjectSummary MapSummary(ObjectRow row)
        => new(
            row.Id,
            PdmMapping.ToObjectType(row.ObjectType),
            row.Designation,
            row.Name,
            row.State is null ? null : PdmMapping.ToObjectState(row.State),
            row.VersionNo);

    public class ObjectRow
    {
        public Guid Id { get; set; }

        public string ObjectType { get; set; } = string.Empty;

        public string? Designation { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? State { get; set; }

        public int? VersionNo { get; set; }
    }

    public sealed class DetailsRow : ObjectRow
    {
        public Guid? VersionId { get; set; }

        public string? Material { get; set; }

        public decimal? MassKg { get; set; }

        public DateTimeOffset? CreatedAt { get; set; }

        public int ChildCount { get; set; }
    }

    public sealed class VersionRow
    {
        public Guid Id { get; set; }

        public Guid ObjectId { get; set; }

        public int VersionNo { get; set; }

        public string State { get; set; } = string.Empty;

        public string? Material { get; set; }

        public decimal? MassKg { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    public sealed class BomTreeRow
    {
        public Guid ObjectId { get; set; }

        public Guid VersionId { get; set; }

        public string ObjectType { get; set; } = string.Empty;

        public string? Designation { get; set; }

        public string Name { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public string? Material { get; set; }

        public decimal? MassKg { get; set; }

        public Guid? ParentVersionId { get; set; }

        public int? Quantity { get; set; }

        public int Depth { get; set; }
    }
}
