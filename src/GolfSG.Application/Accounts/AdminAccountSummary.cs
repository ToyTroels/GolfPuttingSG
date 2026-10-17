namespace GolfSG.Application.Accounts;

public sealed record AdminAccountSummary(string Id, string? Username, bool Locked, DateTimeOffset? LockoutEnd);
