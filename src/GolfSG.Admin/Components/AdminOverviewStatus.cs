namespace GolfSG.Admin.Components;

public sealed record AdminOverviewStatus(string Environment, string? ApiVersion, string? Commit,
    bool DatabaseAvailable, string? DatabaseVersion, string Compatibility);
