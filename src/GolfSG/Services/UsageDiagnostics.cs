using System.Runtime.CompilerServices;
using System.Text.Json;
using GolfSG.Application.ViewModels;
using GolfSG.Application.Diagnostics;
using GolfSG.Core.Models;

namespace GolfSG.Application.Services;

// Best-effort, local-only counters. No entered values or round identifiers are exported.
internal static class UsageDiagnostics
{
    private static readonly object Sync = new();
    private static readonly ConditionalWeakTable<HoleInputViewModel, HoleUsage> Holes = new();
    private static readonly SemaphoreSlim SaveGate = new(1, 1);
    private static UsageReport? report;
    private static string Path => System.IO.Path.Combine(FileSystem.AppDataDirectory, "usage-summary.json");
    public static bool Enabled
    {
        get { try { return Preferences.Default.Get("usage-diagnostics", false); } catch { return false; } }
        set
        {
            try
            {
                Preferences.Default.Set("usage-diagnostics", value);
                lock (Sync) Holes.Clear();
                if (value) StartSession(); else EndSession();
            }
            catch { /* Diagnostics must never interrupt the app. */ }
        }
    }

    private static UsageReport Report => report ??= Load();
    private static UsageReport Load()
    {
        try { return JsonSerializer.Deserialize<UsageReport>(File.ReadAllText(Path)) ?? new(); }
        catch { return new(); }
    }

    public static void StartSession()
    {
        try
        {
            if (!Enabled) return;
            lock (Sync)
            {
                if (Preferences.Default.Get("usage-session-open", false)) Report.UnexpectedSessionEnds++;
                Report.ForegroundSessions++;
                Preferences.Default.Set("usage-session-open", true);
            }
            _ = SaveAsync();
        }
        catch { }
    }

    public static void EndSession()
    {
        try
        {
            Preferences.Default.Set("usage-session-open", false);
            if (report is not null) _ = SaveAsync();
        }
        catch { }
    }

    public static void BeginVisit(HoleInputViewModel hole)
    {
        try
        {
            if (!Enabled) return;
            lock (Sync)
            {
                var usage = Holes.GetValue(hole, _ => new());
                var data = hole.ToHole();
                if (IsComplete(hole, data)) usage.Snapshot.MarkExisting(data);
            }
        }
        catch { }
    }

    private static bool IsComplete(HoleInputViewModel hole, HolePuttingData data) =>
        (!hole.TrackApproach || data.IsApproachCompleted) &&
        (!hole.IsAroundGreenInputVisible || data.IsAroundGreenCompleted) &&
        (!hole.IsPuttingInputVisible || data.IsCompleted);

    public static void Action(HoleInputViewModel hole, bool guided, string method)
    {
        try
        {
            if (!Enabled) return;
            lock (Sync)
            {
                var usage = Holes.GetValue(hole, _ => new());
                var key = $"version={AppInfo.Current.VersionString}+{AppInfo.Current.BuildString}|{(guided ? "guided" : "collapsible")}|putting={hole.TrackPutting}|approach={hole.TrackApproach}|aroundGreen={hole.TrackAroundGreen}";
                if (usage.Group != key) { usage.Group = key; usage.Presses = 0; }
                usage.Presses++;
                var group = GetGroup(key);
                group.InputMethods[method] = group.InputMethods.GetValueOrDefault(method) + 1;
            }
        }
        catch { }
    }

    public static void FinishVisit(HoleInputViewModel hole)
    {
        try
        {
            if (!Enabled) return;
            lock (Sync)
            {
                if (!Holes.TryGetValue(hole, out var usage) || usage.Group is null) return;
                var data = hole.ToHole();
                var complete = IsComplete(hole, data);
                if (!complete) return;
                var group = GetGroup(usage.Group);
                var result = usage.Snapshot.Finish(data, complete);
                if (result == UsageVisitResult.FirstCompletion)
                {
                    group.CompletedHoles++;
                    group.PressesToComplete += usage.Presses;
                }
                else if (result == UsageVisitResult.Correction)
                {
                    group.CorrectionVisits++;
                    group.CorrectionPresses += usage.Presses;
                }
                usage.Presses = 0;
            }
            _ = SaveAsync();
        }
        catch { }
    }

    private static UsageGroup GetGroup(string key)
    {
        if (!Report.Groups.TryGetValue(key, out var group)) Report.Groups[key] = group = new();
        return group;
    }

    public static async Task SaveAsync()
    {
        try
        {
            await SaveGate.WaitAsync().ConfigureAwait(false);
            try
            {
                string json;
                lock (Sync) json = JsonSerializer.Serialize(Report, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path + ".tmp", json).ConfigureAwait(false);
                File.Move(Path + ".tmp", Path, true);
            }
            finally { SaveGate.Release(); }
        }
        catch { }
    }

    public static async Task ExportAsync()
    {
        await SaveAsync();
        var target = System.IO.Path.Combine(FileSystem.CacheDirectory, "golfsg-usage-summary.json");
        await File.WriteAllTextAsync(target, await File.ReadAllTextAsync(Path));
        await Share.Default.RequestAsync(new ShareFileRequest("Brugsoversigt", new ShareFile(target)));
    }

    public static async Task ClearAsync()
    {
        await SaveGate.WaitAsync();
        try
        {
            File.Delete(Path);
            File.Delete(Path + ".tmp");
            File.Delete(System.IO.Path.Combine(FileSystem.CacheDirectory, "golfsg-usage-summary.json"));
            lock (Sync) { report = new(); Holes.Clear(); }
        }
        finally { SaveGate.Release(); }
    }

    private sealed class HoleUsage
    {
        public string? Group;
        public int Presses;
        public HoleUsageSnapshot Snapshot { get; } = new();
    }
    public sealed class UsageReport
    {
        public int SchemaVersion { get; set; } = 1;
        public int ForegroundSessions { get; set; }
        public int UnexpectedSessionEnds { get; set; }
        public string ReliabilityNote { get; set; } = "Unexpected ends may be crashes, force-stops, or OS termination; this is not a crash-free rate.";
        public string MeasurementNote { get; set; } = "Counts button presses and distance edit activations, not keyboard keys or physical screen taps. Correction visits change a previously completed hole. Hole deduplication lasts for this app process.";
        public Dictionary<string, UsageGroup> Groups { get; set; } = new();
    }
    public sealed class UsageGroup
    {
        public int CompletedHoles { get; set; }
        public int PressesToComplete { get; set; }
        public double? AveragePressesPerCompletedHole => CompletedHoles > 0 ? (double)PressesToComplete / CompletedHoles : null;
        public int CorrectionVisits { get; set; }
        public int CorrectionPresses { get; set; }
        public Dictionary<string, int> InputMethods { get; set; } = new();
    }
}
