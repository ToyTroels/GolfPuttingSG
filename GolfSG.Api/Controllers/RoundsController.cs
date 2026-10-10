using System.Text.Json;
using GolfSG.Api.Data;
using GolfSG.Api.Models;
using GolfSG.Application.Rounds.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace GolfSG.Api.Controllers;

[ApiController]
[Route("api/rounds")]
public sealed class RoundsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RemoteRound>>> GetAll(CancellationToken cancellationToken)
    {
        var rounds = await db.SavedRounds.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);
        return Ok(rounds.Select(ToResponse).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RemoteRound>> GetById(string id, CancellationToken cancellationToken)
    {
        var round = await db.SavedRounds.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return round is null ? NotFound() : Ok(ToResponse(round));
    }

    // Upsert the full round or a deletion. ExpectedVersion prevents overwriting a concurrent write.
    [HttpPut("{id}")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<RemoteRound>> Write(string id, RoundWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RoundSyncValidation.IsValidId(id) || request.Version == Guid.Empty ||
            request.ExpectedVersion == Guid.Empty || request.Round is { } data && data.Id != id)
            return BadRequest(new ProblemDetails { Title = "Invalid round ID or version." });

        string? payload = null;
        if (request.Round is { } round)
        {
            try
            {
                RoundSyncValidation.Validate(round);
                payload = JsonSerializer.Serialize(round, RoundSyncJsonContext.Default.Round);
            }
            catch (Exception ex) when (ex is ArgumentException or JsonException or InvalidOperationException)
            { return BadRequest(new ProblemDetails { Title = "Invalid round data." }); }
        }

        // One PostgreSQL statement makes the precondition and write atomic, including first creation.
        // Retrying the same version/payload is safe after a lost HTTP response.
        var parameters = new[]
        {
            new NpgsqlParameter("id", NpgsqlDbType.Varchar) { Value = id },
            new NpgsqlParameter("version", NpgsqlDbType.Uuid) { Value = request.Version },
            new NpgsqlParameter("expected", NpgsqlDbType.Uuid) { Value = (object?)request.ExpectedVersion ?? DBNull.Value },
            new NpgsqlParameter("payload", NpgsqlDbType.Jsonb) { Value = (object?)payload ?? DBNull.Value }
        };
        var written = await db.SavedRounds.FromSqlRaw("""
            INSERT INTO public.saved_rounds AS existing (id, version, payload, updated_at)
            SELECT @id, @version, @payload, CURRENT_TIMESTAMP
            WHERE @expected IS NULL OR EXISTS (SELECT 1 FROM public.saved_rounds WHERE id = @id)
            ON CONFLICT (id) DO UPDATE
            SET version = EXCLUDED.version, payload = EXCLUDED.payload, updated_at = CURRENT_TIMESTAMP
            WHERE (existing.version = @expected AND existing.version <> EXCLUDED.version)
               OR (existing.version = EXCLUDED.version AND existing.payload IS NOT DISTINCT FROM EXCLUDED.payload)
            RETURNING id, version, payload, updated_at
            """, parameters).AsNoTracking().ToListAsync(cancellationToken);
        return written.Count == 0
            ? Conflict(new ProblemDetails { Title = "The round changed. Read its current version and retry." })
            : Ok(ToResponse(written[0]));
    }

    private static RemoteRound ToResponse(SavedRound row) => new(row.Id, row.Version,
        row.Payload is null ? null : JsonSerializer.Deserialize(row.Payload, RoundSyncJsonContext.Default.Round)
            ?? throw new InvalidDataException("Stored round payload is invalid."));
}
