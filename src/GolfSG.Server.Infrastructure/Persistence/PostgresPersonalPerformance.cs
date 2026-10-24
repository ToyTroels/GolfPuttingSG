using System.Text.Json;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Application.Server;
using GolfSG.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Server.Infrastructure.Persistence;

public sealed class PostgresPersonalPerformance(AppDbContext database) : IPersonalPerformance
{
    private static Round? Read(string payload, string id)
    {
        try
        {
            var round = JsonSerializer.Deserialize(payload, RoundSyncJsonContext.Default.Round);
            if (round is null || round.Id != id) return null;
            RoundSyncValidation.Validate(round);
            return round;
        }
        catch (JsonException) { return null; }
        catch (ArgumentException) { return null; }
    }
    public async Task<PersonalPerformance> OverviewAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var payloads = await database.SavedRounds.AsNoTracking().Where(r => r.UserId == accountId && r.Payload != null)
            .OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Id).Select(r => new { r.Id, r.Payload }).Take(30).ToListAsync(cancellationToken);
        var rounds = payloads.Select(p => Read(p.Payload!, p.Id)).OfType<Round>().ToArray();
        return PersonalPerformanceMapper.Overview(rounds, payloads.Count - rounds.Length);
    }
    public async Task<PersonalRoundDetail?> DetailAsync(string accountId, string roundId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var payload = await database.SavedRounds.AsNoTracking().Where(r => r.UserId == accountId && r.Id == roundId && r.Payload != null)
            .Select(r => r.Payload).SingleOrDefaultAsync(cancellationToken);
        var round = payload is null ? null : Read(payload, roundId);
        return round is null ? null : PersonalPerformanceMapper.Detail(round);
    }
}
