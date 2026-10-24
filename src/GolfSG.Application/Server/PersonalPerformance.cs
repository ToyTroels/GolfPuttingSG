namespace GolfSG.Application.Server;

public sealed record PersonalRoundSummary(string Id, string Title, DateTime Date, bool Practice, string Kind,
    int Completed, int Planned, double TotalSg, int Strokes, double? PuttingSg = null, double? ApproachSg = null, double? AroundGreenSg = null);
public sealed record PersonalScoreRow(int Number, string Label, string Description, int Strokes, int Penalties,
    double? PuttingSg, double? ApproachSg, double? AroundGreenSg, double TotalSg);
public sealed record PersonalRoundDetail(PersonalRoundSummary Summary, IReadOnlyList<PersonalScoreRow> Rows);
public sealed record PersonalCategoryScore(string Name, int Rounds, double AverageSg);
public sealed record PersonalPerformance(int Window, int Loaded, int Unavailable, int NormalRounds, int PracticeRounds,
    double TotalSg, double? AverageSg, IReadOnlyList<PersonalRoundSummary> Recent,
    IReadOnlyList<PersonalRoundSummary> Trend, IReadOnlyList<PersonalCategoryScore> Categories,
    IReadOnlyList<PersonalRoundSummary>? Sessions = null);
public interface IPersonalPerformance
{
    Task<PersonalPerformance> OverviewAsync(string accountId, CancellationToken cancellationToken);
    Task<PersonalRoundDetail?> DetailAsync(string accountId, string roundId, CancellationToken cancellationToken);
}
