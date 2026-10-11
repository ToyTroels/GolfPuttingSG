using System.Text.Json;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Application.Server;
using GolfSG.Server.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace GolfSG.Server.Infrastructure.Persistence;

public sealed class PostgresRoundStore(AppDbContext db) : IServerRoundStore
{
    public async Task<IReadOnlyList<RemoteRound>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var rounds = await db.SavedRounds.AsNoTracking().Where(r => r.UserId == userId)
            .OrderBy(r => r.Id).ToListAsync(cancellationToken);
        return rounds.Select(ToResponse).ToList();
    }

    public async Task<RemoteRound?> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var round = await db.SavedRounds.AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == userId && r.Id == id, cancellationToken);
        return round is null ? null : ToResponse(round);
    }

    public async Task<RemoteRound?> WriteAsync(string userId, string id, RoundWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var payload = request.Round is { } round
            ? JsonSerializer.Serialize(round, RoundSyncJsonContext.Default.Round)
            : null;

        // One PostgreSQL statement makes the precondition and write atomic, including first creation.
        // Retrying the same version/payload is safe after a lost HTTP response.
        var parameters = new[]
        {
            new NpgsqlParameter("user_id", NpgsqlDbType.Text) { Value = userId },
            new NpgsqlParameter("id", NpgsqlDbType.Varchar) { Value = id },
            new NpgsqlParameter("version", NpgsqlDbType.Uuid) { Value = request.Version },
            new NpgsqlParameter("expected", NpgsqlDbType.Uuid) { Value = (object?)request.ExpectedVersion ?? DBNull.Value },
            new NpgsqlParameter("payload", NpgsqlDbType.Jsonb) { Value = (object?)payload ?? DBNull.Value }
        };
        var written = await db.SavedRounds.FromSqlRaw("""
            INSERT INTO public.saved_rounds AS existing (user_id, id, version, payload, updated_at)
            SELECT @user_id, @id, @version, @payload, CURRENT_TIMESTAMP
            WHERE @expected IS NULL OR EXISTS (SELECT 1 FROM public.saved_rounds WHERE user_id = @user_id AND id = @id)
            ON CONFLICT (user_id, id) DO UPDATE
            SET version = EXCLUDED.version, payload = EXCLUDED.payload, updated_at = CURRENT_TIMESTAMP
            WHERE (existing.version = @expected AND existing.version <> EXCLUDED.version)
               OR (existing.version = EXCLUDED.version AND existing.payload IS NOT DISTINCT FROM EXCLUDED.payload)
            RETURNING user_id, id, version, payload, updated_at
            """, parameters).AsNoTracking().ToListAsync(cancellationToken);
        return written.Count == 0 ? null : ToResponse(written[0]);
    }

    private static RemoteRound ToResponse(SavedRound row) => new(row.Id, row.Version,
        row.Payload is null ? null : JsonSerializer.Deserialize(row.Payload, RoundSyncJsonContext.Default.Round)
            ?? throw new InvalidDataException("Stored round payload is invalid."));
}
