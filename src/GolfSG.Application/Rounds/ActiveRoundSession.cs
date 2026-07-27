namespace GolfSG.Application.Rounds;

public sealed record ActiveRoundSession(
    RoundDraft Draft,
    int CurrentHoleNumber,
    DateTimeOffset UpdatedAtUtc);
