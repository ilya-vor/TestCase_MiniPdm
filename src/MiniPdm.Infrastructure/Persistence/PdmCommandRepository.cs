using Dapper;
using MiniPdm.Application.Persistence;
using MiniPdm.Domain;
using MiniPdm.Infrastructure.Database;

namespace MiniPdm.Infrastructure.Persistence;

/// <summary>Изменение данных PDM в PostgreSQL.</summary>
public sealed class PdmCommandRepository : IPdmCommandRepository
{
    private readonly IDbConnectionFactory _factory;

    public PdmCommandRepository(IDbConnectionFactory factory)
        => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public async Task<VersionInfo> ChangeStateAsync(Guid versionId, ObjectState target, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        const string selectSql = """
            SELECT id         AS Id,
                   object_id  AS ObjectId,
                   version_no AS VersionNo,
                   state      AS State,
                   material   AS Material,
                   mass_kg    AS MassKg,
                   created_at AS CreatedAt
            FROM object_version
            WHERE id = @VersionId
            FOR UPDATE;
            """;

        var version = await connection.QuerySingleOrDefaultAsync<VersionRow>(
            new CommandDefinition(selectSql, new { VersionId = versionId }, transaction, cancellationToken: ct));

        if (version is null)
        {
            throw new DomainException("Версия не найдена.");
        }

        var current = PdmMapping.ToObjectState(version.State);
        ObjectStateMachine.EnsureCanTransition(current, target);

        const string updateSql = "UPDATE object_version SET state = @State WHERE id = @VersionId;";
        await connection.ExecuteAsync(
            new CommandDefinition(
                updateSql,
                new { State = target.ToDb(), VersionId = versionId },
                transaction,
                cancellationToken: ct));

        // Текущая версия — последняя неаннулированная.
        const string currentSql = """
            SELECT id
            FROM object_version
            WHERE object_id = @ObjectId AND state <> 'Cancelled'
            ORDER BY version_no DESC
            LIMIT 1;
            """;
        var newCurrent = await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(currentSql, new { version.ObjectId }, transaction, cancellationToken: ct));

        const string setCurrentSql = "UPDATE pdm_object SET current_version_id = @Current WHERE id = @ObjectId;";
        await connection.ExecuteAsync(
            new CommandDefinition(
                setCurrentSql,
                new { Current = newCurrent, version.ObjectId },
                transaction,
                cancellationToken: ct));

        await transaction.CommitAsync(ct).ConfigureAwait(false);

        return new VersionInfo(
            version.Id,
            version.ObjectId,
            version.VersionNo,
            target,
            version.Material,
            version.MassKg,
            version.CreatedAt);
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
}
