using System.Security.Cryptography;
using System.Text.Json;
using GolfSG.Core.Models;

namespace GolfSG.Application.Diagnostics;

public enum UsageVisitResult { None, FirstCompletion, Correction }

// Fingerprints exist only in memory; reports contain aggregate counters only.
public sealed class HoleUsageSnapshot
{
    private string? previous;
    public void MarkExisting(HolePuttingData data) => previous ??= Fingerprint(data);
    public UsageVisitResult Finish(HolePuttingData data, bool complete)
    {
        if (!complete) return UsageVisitResult.None;
        var current = Fingerprint(data);
        var result = previous is null ? UsageVisitResult.FirstCompletion
            : previous == current ? UsageVisitResult.None : UsageVisitResult.Correction;
        previous = current;
        return result;
    }
    private static string Fingerprint(HolePuttingData data) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data)));
}
