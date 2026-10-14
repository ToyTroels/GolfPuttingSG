using GolfSG.Application.Services;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Application.Courses;

public sealed class CoursePracticeService(ICoursePracticeRepository store, IRoundRepository rounds,
    IStrokesGainedPuttingService putting, IStrokesGainedApproachService approach, IStrokesGainedAroundGreenService around)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task<CoursePracticeDocument> LoadAsync() => store.LoadAsync();

    public static void ValidateCourse(PracticeCourse course, bool requireShots = true)
    {
        ArgumentNullException.ThrowIfNull(course);
        if (string.IsNullOrWhiteSpace(course.Id) || string.IsNullOrWhiteSpace(course.Name) || course.Name.Length > 100 ||
            string.IsNullOrWhiteSpace(course.ImageFile) || Path.GetFileName(course.ImageFile) != course.ImageFile ||
            course.ImageFile.Contains('/') || course.ImageFile.Contains('\\') ||
            !double.IsFinite(course.ImageAspectRatio) || course.ImageAspectRatio <= 0 ||
            !double.IsFinite(course.MetresPerImageWidth) || course.MetresPerImageWidth is <= 0 or > 10000 ||
            course.Shots is null || course.Shots.Any(s => s is null) || course.Shots.Count > 100 || requireShots && course.Shots.Count == 0)
            throw new ArgumentException("Giv banen et navn, et billede, en gyldig målestok og mindst ét slag.");
        if (course.Shots.Select(s => s.Id).Distinct().Count() != course.Shots.Count)
            throw new ArgumentException("Slag skal have forskellige id'er.");
        if (course.Shots.Sum(shot => (long)shot.Attempts) > 1000)
            throw new ArgumentException("En bane kan højst indeholde 1000 planlagte slag i alt.");
        foreach (var shot in course.Shots)
        {
            if (shot.Attempts is < 1 or > 100)
                throw new ArgumentException("Vælg et antal slag mellem 1 og 100 for hver opgave.");
            if (string.IsNullOrWhiteSpace(shot.Id) || string.IsNullOrWhiteSpace(shot.Name) || shot.Name.Length > 100 ||
                shot.From?.IsValid != true || shot.Target?.IsValid != true ||
                !Enum.IsDefined(shot.Lie) || shot.Lie == ShotLie.Holed || !Enum.IsDefined(shot.Category) ||
                (shot.Category == PracticeShotCategory.Putting) != (shot.Lie == ShotLie.Green) ||
                shot.Category == PracticeShotCategory.AroundGreen && shot.Lie == ShotLie.Tee ||
                shot.MeasuredDistanceMeters is { } measured && (!double.IsFinite(measured) || measured <= 0) ||
                !double.IsFinite(shot.DistanceMeters(course)) || shot.DistanceMeters(course) is < 0.1 or > 1000)
                throw new ArgumentException("Vælg positioner, lie og kategori samt en afstand mellem 0,1 og 1000 m.");
        }
    }

    public static void ValidateResults(PracticeCourse course, IReadOnlyList<CourseShotResult> results,
        IReadOnlyList<CoursePracticeShot>? orderedShots = null, bool requireComplete = false)
    {
        ValidateCourse(course);
        ArgumentNullException.ThrowIfNull(results);
        var order = orderedShots ?? course.Shots;
        var counts = course.Shots.ToDictionary(shot => shot.Id, _ => 0, StringComparer.Ordinal);
        foreach (var (result, index) in results.Select((result, index) => (result, index)))
        {
            if (result is null || !double.IsFinite(result.StrokesGained) || !double.IsFinite(result.ExpectedStart) ||
                !double.IsFinite(result.ExpectedFinish) || result.Outcome is not { } outcome ||
                !double.IsFinite(outcome.EndDistanceMeters) || outcome.EndDistanceMeters is < 0 or > 1000 ||
                outcome.PenaltyStrokes is < 0 or > 10 || !Enum.IsDefined(outcome.EndLie) ||
                outcome.Holed != (outcome.EndLie == ShotLie.Holed) ||
                (outcome.Holed ? outcome.EndDistanceMeters != 0 : outcome.EndDistanceMeters <= 0))
                throw new ArgumentException("Et slagresultat indeholder ugyldige værdier.");
            var shotId = result.ShotId ?? (index < order.Count ? order[index].Id : null);
            if (shotId is null || !counts.TryGetValue(shotId, out var count))
                throw new ArgumentException("Et slagresultat henviser til en ukendt opgave.");
            var shot = course.Shots.First(shot => shot.Id == shotId);
            if (count >= shot.Attempts)
                throw new ArgumentException("Der er registreret flere slag end planlagt for en opgave.");
            counts[shotId] = count + 1;
        }
        if (requireComplete && course.Shots.Any(shot => counts[shot.Id] != shot.Attempts))
            throw new ArgumentException("Runden mangler et eller flere planlagte slag.");
    }

    public async Task SaveCourseAsync(PracticeCourse course)
    {
        ValidateCourse(course);
        await gate.WaitAsync();
        try
        {
            var document = await store.LoadAsync();
            var courses = document.Courses.Where(c => c.Id != course.Id).Append(course).ToArray();
            await store.SaveAsync(document with { Courses = courses });
        }
        finally { gate.Release(); }
    }

    public async Task<PracticeCourse> ImportCourseAsync(PracticeCourse course)
    {
        ValidateCourse(course);
        await gate.WaitAsync();
        try
        {
            var document = await store.LoadAsync();
            var existing = document.Courses.FirstOrDefault(c => c.Id == course.Id);
            if (existing is not null) return existing;
            await store.SaveAsync(document with { Courses = document.Courses.Append(course).ToArray() });
            return course;
        }
        finally { gate.Release(); }
    }

    public async Task<CoursePracticeSession> StartAsync(PracticeCourse course)
        => await StartSessionAsync(course, Guid.NewGuid().ToString("N"), null);

    public Task<CoursePracticeSession> StartGameAsync(PracticeCourse course, string roundId, CourseGameLink game)
        => StartSessionAsync(course, roundId, game);

    private async Task<CoursePracticeSession> StartSessionAsync(PracticeCourse course, string roundId, CourseGameLink? game)
    {
        ValidateCourse(course);
        ValidateGameLink(game);
        if (game is not null && !Guid.TryParseExact(roundId, "N", out _)) throw new ArgumentException("Spillets runde-id er ugyldigt.");
        await gate.WaitAsync();
        try
        {
            var document = await store.LoadAsync();
            if (game is not null && document.Active is { } current && current.Id == roundId && current.Game == game)
                return current;
            if (document.Active is not null) throw new InvalidOperationException("Fortsæt, afslut eller opgiv den igangværende træningsrunde først.");
            if (game is not null && await rounds.GetRoundAsync(roundId) is not null)
                throw new InvalidOperationException("Dit resultat er allerede gemt. Åbn spillet for at indsende det.");
            // Freeze the layout and scale for this round, even if the reusable course is edited later.
            var session = new CoursePracticeSession(roundId, DateTime.Now,
                course with { Shots = course.Shots.ToArray() }, [], new(), course.Shots.Select(shot => shot.Id).ToArray(), game);
            await store.SaveAsync(document with { Active = session });
            return session;
        }
        finally { gate.Release(); }
    }

    public static void ValidateGameLink(CourseGameLink? game)
    {
        if (game is null) return;
        if (!Guid.TryParseExact(game.GameId, "N", out var id) || id == Guid.Empty || string.IsNullOrWhiteSpace(game.AccountId) || game.AccountId.Length > 200 ||
            !Uri.TryCreate(game.ServerAddress, UriKind.Absolute, out var server) || server.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(server.UserInfo))
            throw new ArgumentException("Rundens spiltilknytning er ugyldig.");
    }

    public async Task<CoursePracticeSession> SelectShotAsync(string sessionId, string shotId, int expectedIndex, string expectedCurrentShotId)
    {
        await gate.WaitAsync();
        try
        {
            var document = await store.LoadAsync();
            var active = RequireActive(document, sessionId);
            if (active.IsComplete || active.Results.Count != expectedIndex || active.CurrentShot?.Id != expectedCurrentShotId)
                throw new InvalidOperationException("Det aktuelle slag er ændret. Genåbn runden.");
            var selected = active.Course.Shots.FirstOrDefault(shot => shot.Id == shotId);
            if (selected is null) throw new InvalidOperationException("Slaget findes ikke på denne bane.");
            if (active.CompletedAttempts(shotId) >= selected.Attempts)
                throw new InvalidOperationException("Alle planlagte slag for denne opgave er allerede spillet.");
            if (active.CurrentShot!.Id == shotId) return active;

            // Bind legacy positional results before changing the task order.
            var updated = active with { Results = BindResults(active) };
            updated = updated with { ShotOrder = SelectRemainingTask(updated, shotId), Input = new() };
            await store.SaveAsync(document with { Active = updated });
            return updated;
        }
        finally { gate.Release(); }
    }

    public async Task<CoursePracticeSession> UpdateAttemptsAsync(string sessionId, string shotId, int attempts,
        int expectedIndex, string? expectedCurrentShotId, int expectedAttempts)
    {
        await gate.WaitAsync();
        try
        {
            var document = await store.LoadAsync();
            var active = RequireActive(document, sessionId);
            if (active.Game is not null)
                throw new InvalidOperationException("Antallet af slag er fast for alle deltagere i spillet.");
            if (active.IsComplete || active.Results.Count != expectedIndex || active.CurrentShot?.Id != expectedCurrentShotId)
                throw new InvalidOperationException("Det aktuelle slag er ændret. Genåbn runden.");
            var selected = active.Course.Shots.FirstOrDefault(shot => shot.Id == shotId);
            if (selected is null) throw new InvalidOperationException("Slaget findes ikke på denne bane.");
            if (selected.Attempts != expectedAttempts)
                throw new InvalidOperationException("Antallet af slag er ændret. Genåbn runden.");
            if (active.CompletedAttempts(shotId) != 0)
                throw new InvalidOperationException("Antallet af slag kan kun ændres, før du registrerer det første slag fra denne position.");

            var course = active.Course with
            {
                Shots = active.Course.Shots.Select(shot => shot.Id == shotId ? shot with { Attempts = attempts } : shot).ToArray()
            };
            ValidateCourse(course);
            if (selected.Attempts == attempts) return active;
            var updated = active with { Course = course };
            await store.SaveAsync(document with { Active = updated });
            return updated;
        }
        finally { gate.Release(); }
    }

    public async Task SaveInputAsync(string sessionId, CourseResultInput input, int expectedIndex, string? expectedShotId = null)
    {
        await gate.WaitAsync();
        try
        {
            var document = await store.LoadAsync();
            if (document.Active is not { } active || active.Id != sessionId || active.IsComplete || active.Results.Count != expectedIndex ||
                expectedShotId is not null && active.CurrentShot?.Id != expectedShotId) return;
            await store.SaveAsync(document with { Active = active with { Input = input } });
        }
        finally { gate.Release(); }
    }

    public CourseShotResult Score(CoursePracticeShot shot, PracticeCourse course, CourseShotOutcome outcome)
        => new CoursePracticeScorer(putting, approach, around).Score(shot, course, outcome);

    public async Task<CoursePracticeSession> RecordAsync(string sessionId, int expectedIndex, CourseShotOutcome outcome, string? expectedShotId = null)
    {
        await gate.WaitAsync();
        try
        {
            var document = await store.LoadAsync();
            var active = RequireActive(document, sessionId);
            if (active.IsComplete || active.Results.Count != expectedIndex || expectedShotId is not null && active.CurrentShot?.Id != expectedShotId)
                throw new InvalidOperationException("Resultatet er allerede registreret. Genåbn runden.");
            var result = Score(active.CurrentShot!, active.Course, outcome);
            var updated = active with { Results = active.Results.Append(result).ToArray(), Input = new() };
            // Persist the final result before archiving so a failed archive can be retried after restart.
            await store.SaveAsync(document with { Active = updated });
            return updated;
        }
        finally { gate.Release(); }
    }

    public async Task<CoursePracticeSession> UndoAsync(string sessionId)
    {
        await gate.WaitAsync();
        try
        {
            var document = await store.LoadAsync();
            var active = RequireActive(document, sessionId);
            if (active.Results.Count == 0) return active;
            var results = BindResults(active);
            var previous = results[^1];
            var updated = active with { Results = results.Take(results.Count - 1).ToArray(),
                Input = new(previous.Outcome.EndDistanceMeters.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture),
                    previous.Outcome.EndLie, previous.Outcome.PenaltyStrokes, previous.Outcome.Holed) };
            updated = updated with { ShotOrder = SelectRemainingTask(updated, previous.ShotId!) };
            await store.SaveAsync(document with { Active = updated });
            return updated;
        }
        finally { gate.Release(); }
    }

    public Task<Round> FinishAsync(string sessionId) => ArchiveAsync(sessionId, allowEarlyFinish: false);

    public Task<Round> FinishEarlyAsync(string sessionId) => ArchiveAsync(sessionId, allowEarlyFinish: true);

    private async Task<Round> ArchiveAsync(string sessionId, bool allowEarlyFinish)
    {
        await gate.WaitAsync();
        try
        {
            var document = await store.LoadAsync();
            var active = RequireActive(document, sessionId);
            if (!active.IsComplete && (!allowEarlyFinish || active.Game is not null))
                throw new InvalidOperationException("Registrer alle planlagte slag først.");
            if (active.Results.Count == 0)
                throw new InvalidOperationException("Registrer mindst ét slag, før du gemmer rundens resultat.");
            var round = BuildRound(active);
            await rounds.SaveRoundAsync(round); // Stable id makes recovery retries idempotent.
            await store.SaveAsync(document with { Active = null });
            return round;
        }
        finally { gate.Release(); }
    }

    public async Task DiscardAsync(string sessionId)
    {
        await gate.WaitAsync();
        try
        {
            var document = await store.LoadAsync();
            RequireActive(document, sessionId);
            await store.SaveAsync(document with { Active = null });
        }
        finally { gate.Release(); }
    }

    private static CoursePracticeSession RequireActive(CoursePracticeDocument document, string id) =>
        document.Active is { } active && active.Id == id ? active : throw new InvalidOperationException("Runden er ikke længere aktiv.");

    public static void ValidateArchivedRound(Round round)
    {
        if (round.CoursePractice is not { } practice) return;
        ValidateResults(practice.Course, practice.Results, requireComplete: !round.EndedEarly);
        ValidateGameLink(practice.Game);
        if (round.Holes is null || round.Holes.Count != practice.Results.Count ||
            round.ConfiguredHoleCount != practice.Course.Shots.Sum(shot => shot.Attempts) ||
            practice.Results.Count == 0 ||
            round.EndedEarly && (practice.Game is not null || practice.Results.Count >= round.ConfiguredHoleCount))
            throw new ArgumentException("Antallet af slag i rundens oversigt stemmer ikke med resultaterne.");
    }

    private static IReadOnlyList<CourseShotResult> BindResults(CoursePracticeSession session)
    {
        var shots = session.PlayedShots;
        return session.Results.Select((result, index) => result.ShotId is null ? result with { ShotId = shots[index].Id } : result).ToArray();
    }

    private static IReadOnlyList<string> SelectRemainingTask(CoursePracticeSession session, string shotId)
    {
        var order = session.OrderedShots;
        var counts = session.PlayedShots.GroupBy(shot => shot.Id).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var remainingSlots = Enumerable.Range(0, order.Count).Where(index => counts.GetValueOrDefault(order[index].Id) < order[index].Attempts).ToArray();
        var remainingIds = remainingSlots.Select(index => order[index].Id).ToList();
        remainingIds.Remove(shotId);
        remainingIds.Insert(0, shotId);
        var ids = order.Select(shot => shot.Id).ToArray();
        for (var index = 0; index < remainingSlots.Length; index++) ids[remainingSlots[index]] = remainingIds[index];
        return ids;
    }

    private static Round BuildRound(CoursePracticeSession session)
    {
        var shots = session.PlayedShots;
        var results = BindResults(session);
        var holes = results.Select((r, i) =>
        {
            var shot = shots[i];
            var d = shot.DistanceMeters(session.Course);
            var hole = new HolePuttingData(i + 1, 0, 0, 0, 0);
            return shot.Category switch
            {
                PracticeShotCategory.Putting => hole with { FirstPuttDistanceMeters = d, Putts = 1, ExpectedPutts = r.ExpectedStart, StrokesGainedPutting = r.StrokesGained },
                PracticeShotCategory.Approach => hole with { ApproachDistanceMeters = d, ApproachShots = 1, ExpectedApproachShots = r.ExpectedStart, ExpectedApproachFinishStrokes = r.ExpectedFinish, StrokesGainedApproach = r.StrokesGained },
                _ => hole with { AroundGreenStartDistanceYards = d / .9144, AroundGreenStartLie = shot.Lie, ExpectedAroundGreenStartStrokes = r.ExpectedStart, ExpectedAroundGreenFinishStrokes = r.ExpectedFinish, StrokesGainedAroundGreen = r.StrokesGained }
            };
        }).ToArray();
        return new(session.Id, session.Date, holes, new(shots.Any(s => s.Category == PracticeShotCategory.Putting),
            shots.Any(s => s.Category == PracticeShotCategory.Approach), shots.Any(s => s.Category == PracticeShotCategory.AroundGreen)),
            session.TotalAttempts, EndedEarly: !session.IsComplete,
            CoursePractice: new(session.IsComplete
                ? session.Course with { Shots = shots.DistinctBy(shot => shot.Id, StringComparer.Ordinal).ToArray() }
                : session.Course, results, session.Game));
    }
}
