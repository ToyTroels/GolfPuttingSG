using GolfSG.Application.Courses;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Persistence;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GolfSG.Tests;

[TestClass]
public sealed class CoursePracticeTests
{
    private sealed class Store : ICoursePracticeRepository
    {
        public CoursePracticeDocument Document = CoursePracticeDocument.Empty;
        public Exception? Failure;
        public Task<CoursePracticeDocument> LoadAsync() => Task.FromResult(Document);
        public Task SaveAsync(CoursePracticeDocument document)
        {
            if (Failure is not null) throw Failure;
            Document = document; return Task.CompletedTask;
        }
    }
    private static PracticeCourse Course(params CoursePracticeShot[] shots) => new("course", "Practice facility", "sample.jpg", .5, 100, "Measured reference", shots.Length > 0 ? shots :
        [new("shot", "Approach", new(.5, .8), new(.5, .25), ShotLie.Rough, PracticeShotCategory.Approach)]);
    private static CoursePracticeService Service(ICoursePracticeRepository store, IRoundRepository? rounds = null)
    {
        var putting = new StrokesGainedPuttingService(); var around = new StrokesGainedAroundGreenService(putting);
        return new(store, rounds ?? new TestRoundRepository(), putting, new StrokesGainedApproachService(putting, around), around);
    }

    private static PracticeCourse SelectableCourse() => Course(
        new("approach", "Long approach", new(.5, .8), new(.5, .25), ShotLie.Rough, PracticeShotCategory.Approach),
        new("bunker", "Bunker shot", new(.3, .5), new(.4, .45), ShotLie.Sand, PracticeShotCategory.AroundGreen, 15),
        new("putt", "Putt", new(.5, .2), new(.5, .175), ShotLie.Green, PracticeShotCategory.Putting, 5));

    private static PracticeCourse RepeatedCourse() => SelectableCourse() with
    {
        Shots = SelectableCourse().Shots.Select(s => s with { Attempts = s.Id == "putt" ? 1 : 2 }).ToArray()
    };

    private static string LegacyDocumentJson(int schemaVersion, PracticeCourse course, CoursePracticeSession active)
    {
        var legacy = JsonSerializer.SerializeToNode(new { schemaVersion, data = new CoursePracticeDocument([course], active) },
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        foreach (var savedCourse in legacy["data"]!["courses"]!.AsArray().Append(legacy["data"]!["active"]!["course"]))
            foreach (var shot in savedCourse!["shots"]!.AsArray()) shot!.AsObject().Remove("attempts");
        foreach (var result in legacy["data"]!["active"]!["results"]!.AsArray()) result!.AsObject().Remove("shotId");
        if (schemaVersion == 1) legacy["data"]!["active"]!.AsObject().Remove("shotOrder");
        return legacy.ToJsonString();
    }

    private static void AssertShotOrder(CoursePracticeSession session, params string[] ids) =>
        CollectionAssert.AreEqual(ids, session.OrderedShots.Select(s => s.Id).ToArray());

    [TestMethod]
    public void DistanceUsesImageAspectRatioAndNormalizedCoordinates()
    {
        var course = Course(); Assert.AreEqual(110, course.Shots[0].DistanceMeters(course), .0001);
        var measured = course.Shots[0] with { MeasuredDistanceMeters = 112 };
        Assert.AreEqual(112, measured.DistanceMeters(course));
        var diagonal = measured with { From = new(0, 0), Target = new(1, 1), MeasuredDistanceMeters = null };
        Assert.AreEqual(Math.Sqrt(100 * 100 + 200 * 200), diagonal.DistanceMeters(course), .0001);
    }
    [TestMethod]
    public async Task SetupIsFrozenAndInputResumes()
    {
        var store = new Store(); var service = Service(store); var course = Course(); await service.SaveCourseAsync(course);
        var active = await service.StartAsync(course); await service.SaveInputAsync(active.Id, new("3,5", ShotLie.Sand, 1), 0);
        await service.SaveCourseAsync(course with { Name = "Changed", MetresPerImageWidth = 200 });
        var restored = (await Service(store).LoadAsync()).Active!;
        Assert.AreEqual("Practice facility", restored.Course.Name); Assert.AreEqual(100, restored.Course.MetresPerImageWidth);
        Assert.AreEqual("3,5", restored.Input.Distance); Assert.AreEqual(ShotLie.Sand, restored.Input.Lie);
    }
    [TestMethod]
    public async Task ActiveRoundCannotBeOverwritten()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(Course());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.StartAsync(Course()));
        Assert.AreEqual(active.Id, store.Document.Active!.Id);
    }
    [TestMethod]
    public async Task StaleInputCannotOverwriteTheNextShotsDraft()
    {
        var store = new Store(); var service = Service(store); var course = Course();
        course = course with { Shots = [course.Shots[0], course.Shots[0] with { Id = "second" }] };
        var active = await service.StartAsync(course); await service.RecordAsync(active.Id, 0, new(3, ShotLie.Green, 0, false));
        await service.SaveInputAsync(active.Id, new("2", ShotLie.Green), 1);
        await service.SaveInputAsync(active.Id, new("stale", ShotLie.Rough), 0);
        Assert.AreEqual("2", store.Document.Active!.Input.Distance);
    }
    [TestMethod]
    public async Task DuplicateResultIsRejectedAndUndoRestoresInput()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(Course());
        active = await service.RecordAsync(active.Id, 0, new(3.5, ShotLie.Green, 1, false));
        Assert.IsTrue(active.IsComplete);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RecordAsync(active.Id, 0, new(1, ShotLie.Green, 0, false)));
        active = await service.UndoAsync(active.Id); Assert.IsEmpty(active.Results); Assert.AreEqual(1, active.Input.Penalties);
        Assert.AreEqual(ShotLie.Green, active.Input.Lie); Assert.IsFalse(active.IsComplete);
    }
    [TestMethod]
    public async Task FailedAutosaveDoesNotAdvanceRound()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(Course());
        store.Failure = new IOException(); await Assert.ThrowsExactlyAsync<IOException>(() => service.RecordAsync(active.Id, 0, new(3, ShotLie.Green, 0, false)));
        Assert.IsEmpty(store.Document.Active!.Results);
    }
    [TestMethod]
    public async Task FailedHistorySaveLeavesCompletedRoundForRetry()
    {
        var store = new Store(); var rounds = new TestRoundRepository(); var service = Service(store, rounds);
        var active = await service.StartAsync(Course()); await service.RecordAsync(active.Id, 0, new(0, ShotLie.Holed, 0, true));
        rounds.SaveException = new IOException(); await Assert.ThrowsExactlyAsync<IOException>(() => service.FinishAsync(active.Id));
        Assert.IsTrue(store.Document.Active!.IsComplete); rounds.SaveException = null;
        var saved = await Service(store, rounds).FinishAsync(active.Id);
        Assert.IsNull(store.Document.Active); Assert.AreEqual(active.Id, saved.Id); Assert.AreEqual(active.Date, saved.Date);
        Assert.HasCount(1, rounds.Rounds); Assert.AreEqual("Practice facility", saved.CoursePractice!.Course.Name);
    }
    [TestMethod]
    public async Task ArchiveRetryAfterDraftClearFailureDoesNotDuplicateHistory()
    {
        var store = new Store(); var rounds = new TestRoundRepository(); var service = Service(store, rounds);
        var active = await service.StartAsync(Course()); await service.RecordAsync(active.Id, 0, new(0, ShotLie.Holed, 0, true));
        store.Failure = new IOException(); await Assert.ThrowsExactlyAsync<IOException>(() => service.FinishAsync(active.Id));
        Assert.HasCount(1, rounds.Rounds); store.Failure = null;
        await service.FinishAsync(active.Id); Assert.HasCount(1, rounds.Rounds); Assert.IsNull(store.Document.Active);
    }
    [TestMethod]
    public void ScoringUsesStartAndFinishExpectationsWithPenalties()
    {
        var course = Course(); var service = Service(new Store());
        var r = service.Score(course.Shots[0], course, new(3, ShotLie.Green, 2, false));
        Assert.AreEqual(r.ExpectedStart - r.ExpectedFinish - 3, r.StrokesGained, .0001);
        var holed = service.Score(course.Shots[0], course, new(0, ShotLie.Holed, 0, true));
        Assert.AreEqual(0, holed.ExpectedFinish); Assert.AreEqual(holed.ExpectedStart - 1, holed.StrokesGained);
    }
    [TestMethod]
    public void PuttingAttemptIsScoredAsOneShotWithRemainingPuttValue()
    {
        var shot = new CoursePracticeShot("putt", "Putt", new(.5, .1), new(.5, .2), ShotLie.Green, PracticeShotCategory.Putting, 5);
        var course = Course(shot); var service = Service(new Store());
        var missed = service.Score(shot, course, new(1, ShotLie.Green, 0, false));
        var holed = service.Score(shot, course, new(0, ShotLie.Holed, 0, true));
        Assert.AreEqual(missed.ExpectedStart - missed.ExpectedFinish - 1, missed.StrokesGained, .0001);
        Assert.IsGreaterThan(missed.StrokesGained, holed.StrokesGained);
    }
    [TestMethod]
    public void InvalidCoordinatesDistancesLiesAndOutcomesAreRejected()
    {
        var course = Course(); var shot = course.Shots[0]; var service = Service(new Store());
        foreach (var invalid in new[] { shot with { From = new(double.NaN, 0) }, shot with { MeasuredDistanceMeters = -1 }, shot with { Lie = ShotLie.Green } })
            Assert.ThrowsExactly<ArgumentException>(() => CoursePracticeService.ValidateCourse(course with { Shots = [invalid] }));
        foreach (var invalid in new[] { new CourseShotOutcome(double.NaN, ShotLie.Green, 0, false), new(0, ShotLie.Green, 0, false), new(1, ShotLie.Holed, 0, false), new(1, ShotLie.Green, -1, false) })
            Assert.ThrowsExactly<ArgumentException>(() => service.Score(shot, course, invalid));
    }
    [TestMethod]
    public async Task PracticeHistoryHasSeparateLabelAndDoesNotChangeNormalStatistics()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(Course());
        await service.RecordAsync(active.Id, 0, new(1, ShotLie.Green, 0, false)); var round = await service.FinishAsync(active.Id);
        var item = new RoundListItemViewModel(round); Assert.AreEqual("Banetræning · Beta", item.HistoryTypeText);
        Assert.Contains("1 planlagte slag", item.DetailText);
        Assert.AreEqual(0, RoundStatisticsService.Summarize([round], StrokesGainedCategory.Total).TrackedRoundCount);
    }
    [TestMethod]
    public async Task FileStorageRecoversBackupWithoutRevivingDiscardedSession()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileCoursePracticeRepository(directory); var service = Service(store);
            await service.SaveCourseAsync(Course()); var active = await service.StartAsync(Course());
            await File.WriteAllTextAsync(Path.Combine(directory, "course-practice.json"), "broken");
            Assert.AreEqual(active.Id, (await new FileCoursePracticeRepository(directory).LoadAsync()).Active!.Id);
            await service.DiscardAsync(active.Id); await File.WriteAllTextAsync(Path.Combine(directory, "course-practice.json"), "broken");
            Assert.IsNull((await store.LoadAsync()).Active); Assert.HasCount(1, (await store.LoadAsync()).Courses);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [TestMethod]
    [DataRow(4)]
    [DataRow(999)]
    public async Task FutureSchemaIsPreserved(int schemaVersion)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "course-practice.json"); var original = $"{{\"schemaVersion\":{schemaVersion},\"data\":{{}}}}";
            await File.WriteAllTextAsync(path, original);
            var store = new FileCoursePracticeRepository(directory);
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => store.LoadAsync());
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => store.SaveAsync(CoursePracticeDocument.Empty));
            Assert.AreEqual(original, await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(directory, true); }
    }
    [TestMethod]
    public async Task FinishedPracticeDetailsSurviveRoundExportImport()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new FileRoundRepository(directory); var store = new Store(); var service = Service(store, repository);
            var active = await service.StartAsync(Course()); await service.RecordAsync(active.Id, 0, new(2.2, ShotLie.Green, 1, false));
            var round = await service.FinishAsync(active.Id); var export = Path.Combine(directory, "export.json"); await repository.ExportRoundsAsync(export);
            var imported = new FileRoundRepository(Path.Combine(directory, "imported")); await imported.ImportRoundsAsync(export);
            var loaded = await imported.GetRoundAsync(round.Id);
            Assert.AreEqual(2.2, loaded!.CoursePractice!.Results[0].Outcome.EndDistanceMeters); Assert.AreEqual(110, loaded.CoursePractice.Course.Shots[0].DistanceMeters(loaded.CoursePractice.Course), .0001);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task NewAndLegacySessionsExposeTheCurrentShotInOriginalOrder()
    {
        var course = SelectableCourse(); var active = await Service(new Store()).StartAsync(course);
        CollectionAssert.AreEqual(new[] { "approach", "bunker", "putt" }, active.ShotOrder!.ToArray());
        AssertShotOrder(active, "approach", "bunker", "putt"); Assert.AreEqual("approach", active.CurrentShot!.Id);
        var legacy = new CoursePracticeSession("legacy", active.Date, course, [], new());
        Assert.IsNull(legacy.ShotOrder); AssertShotOrder(legacy, "approach", "bunker", "putt");
        Assert.AreEqual("approach", legacy.CurrentShot!.Id);
        Assert.IsNull((active with { Results = Enumerable.Repeat(new CourseShotResult(new(0, ShotLie.Holed, 0, true), 1, 0, 0), 3).ToArray() }).CurrentShot);
    }

    [TestMethod]
    public async Task ChosenShotPersistsAfterRestartWithoutChangingFrozenGeometryOrLibrary()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileCoursePracticeRepository(directory); var service = Service(store); var course = SelectableCourse();
            await service.SaveCourseAsync(course); var active = await service.StartAsync(course);
            active = await service.SelectShotAsync(active.Id, "putt", 0, "approach");
            await service.SaveInputAsync(active.Id, new("0,8", ShotLie.Green, 1), 0, "putt");
            await service.SaveCourseAsync(course with { Name = "Changed", MetresPerImageWidth = 200,
                Shots = course.Shots.Select(s => s with { From = new(.1, .1) }).ToArray() });
            var restored = (await Service(new FileCoursePracticeRepository(directory)).LoadAsync()).Active!;
            AssertShotOrder(restored, "putt", "approach", "bunker"); Assert.AreEqual("putt", restored.CurrentShot!.Id);
            Assert.AreEqual("0,8", restored.Input.Distance); Assert.AreEqual(1, restored.Input.Penalties);
            Assert.AreEqual("Practice facility", restored.Course.Name); Assert.AreEqual(100, restored.Course.MetresPerImageWidth);
            CollectionAssert.AreEqual(course.Shots.ToArray(), restored.Course.Shots.ToArray());
            CollectionAssert.AreEqual(new[] { "approach", "bunker", "putt" }, (await store.LoadAsync()).Courses[0].Shots.Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "approach", "bunker", "putt" }, course.Shots.Select(s => s.Id).ToArray());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task SelectingCurrentShotPreservesInputWhileAnotherShotStartsWithEmptyInput()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(SelectableCourse());
        var input = new CourseResultInput("3,5", ShotLie.Sand, 2);
        await service.SaveInputAsync(active.Id, input, 0, "approach");
        active = await service.SelectShotAsync(active.Id, "approach", 0, "approach");
        Assert.AreEqual(input, active.Input); AssertShotOrder(active, "approach", "bunker", "putt");
        active = await service.SelectShotAsync(active.Id, "bunker", 0, "approach");
        Assert.AreEqual(new CourseResultInput(), active.Input); Assert.AreEqual("bunker", active.CurrentShot!.Id);
        Assert.AreEqual(new CourseResultInput(), store.Document.Active!.Input);
    }

    [TestMethod]
    public async Task ChoosingRemainingShotKeepsPlayedPrefixAndOtherRemainingShotsInOrder()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(SelectableCourse());
        active = await service.SelectShotAsync(active.Id, "putt", 0, "approach");
        active = await service.RecordAsync(active.Id, 0, new(1, ShotLie.Green, 0, false), "putt"); var played = active.Results[0];
        active = await service.SelectShotAsync(active.Id, "bunker", 1, "approach");
        AssertShotOrder(active, "putt", "bunker", "approach"); Assert.AreEqual(played, active.Results[0]);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.SelectShotAsync(active.Id, "putt", 1, "bunker"));
        AssertShotOrder(store.Document.Active!, "putt", "bunker", "approach"); Assert.AreEqual(played, store.Document.Active!.Results[0]);
    }

    [TestMethod]
    public async Task ShotSelectionRejectsUnknownCompletedAndStaleRequestsWithoutChangingSession()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(SelectableCourse());
        await service.SaveInputAsync(active.Id, new("2", ShotLie.Green), 0, "approach"); var before = store.Document;
        foreach (var request in new[]
        {
            (Id: active.Id, Shot: "missing", Index: 0, Current: "approach"),
            (Id: active.Id, Shot: "putt", Index: 1, Current: "approach"),
            (Id: active.Id, Shot: "putt", Index: 0, Current: "bunker"),
            (Id: "another-session", Shot: "putt", Index: 0, Current: "approach")
        })
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.SelectShotAsync(request.Id, request.Shot, request.Index, request.Current));
            Assert.AreSame(before, store.Document);
        }
        for (var index = 0; index < active.Course.Shots.Count; index++)
            active = await service.RecordAsync(active.Id, index, new(0, ShotLie.Holed, 0, true), active.CurrentShot!.Id);
        before = store.Document;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.SelectShotAsync(active.Id, "approach", 3, "approach"));
        Assert.AreSame(before, store.Document); Assert.IsTrue(active.IsComplete); Assert.IsNull(active.CurrentShot);
    }

    [TestMethod]
    public async Task LateInputAndResultForAnotherShotAreRejectedEvenWhenResultCountMatches()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(SelectableCourse());
        active = await service.SelectShotAsync(active.Id, "putt", 0, "approach");
        var input = new CourseResultInput("0,5", ShotLie.Green, 1); await service.SaveInputAsync(active.Id, input, 0, "putt");
        await service.SaveInputAsync(active.Id, new("stale", ShotLie.Sand, 2), 0, "approach");
        Assert.AreEqual(input, store.Document.Active!.Input);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RecordAsync(active.Id, 0, new(3, ShotLie.Green, 0, false), "approach"));
        Assert.IsEmpty(store.Document.Active!.Results); Assert.AreEqual(input, store.Document.Active.Input);
        active = await service.RecordAsync(active.Id, 0, new(.5, ShotLie.Green, 1, false), "putt");
        Assert.AreEqual(Service(new Store()).Score(active.OrderedShots[0], active.Course, new(.5, ShotLie.Green, 1, false)), active.Results[0]);
    }

    [TestMethod]
    public async Task UndoRestoresLastChosenShotAndItsResultInputAfterFurtherSelection()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(SelectableCourse());
        active = await service.SelectShotAsync(active.Id, "putt", 0, "approach");
        active = await service.RecordAsync(active.Id, 0, new(.75, ShotLie.Green, 2, false), "putt");
        active = await service.SelectShotAsync(active.Id, "bunker", 1, "approach");
        await service.SaveInputAsync(active.Id, new("new draft", ShotLie.Sand), 1, "bunker");
        active = await service.UndoAsync(active.Id);
        Assert.IsEmpty(active.Results); Assert.AreEqual("putt", active.CurrentShot!.Id); AssertShotOrder(active, "putt", "bunker", "approach");
        Assert.AreEqual(.75.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture), active.Input.Distance);
        Assert.AreEqual(ShotLie.Green, active.Input.Lie); Assert.AreEqual(2, active.Input.Penalties); Assert.IsFalse(active.Input.Holed);
        active = await service.RecordAsync(active.Id, 0, new(0, ShotLie.Holed, 0, true), "putt");
        Assert.AreEqual("bunker", active.CurrentShot!.Id);
    }

    [TestMethod]
    public async Task FailedShotSelectionSavePreservesPreviousOrderAndDraft()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(SelectableCourse());
        await service.SaveInputAsync(active.Id, new("3", ShotLie.Green, 1), 0, "approach"); var before = store.Document;
        store.Failure = new IOException();
        await Assert.ThrowsExactlyAsync<IOException>(() => service.SelectShotAsync(active.Id, "putt", 0, "approach"));
        Assert.AreSame(before, store.Document); AssertShotOrder(store.Document.Active!, "approach", "bunker", "putt");
        Assert.AreEqual("3", store.Document.Active!.Input.Distance);
        store.Failure = null; active = await service.SelectShotAsync(active.Id, "putt", 0, "approach");
        Assert.AreEqual("putt", active.CurrentShot!.Id); Assert.AreEqual(new CourseResultInput(), active.Input);
    }

    [TestMethod]
    public async Task ScoringHistoryAndExportUseChosenPlayOrderAcrossDifferentShotCategories()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new FileRoundRepository(directory); var store = new Store(); var service = Service(store, repository);
            var course = SelectableCourse(); await service.SaveCourseAsync(course); var active = await service.StartAsync(course);
            active = await service.SelectShotAsync(active.Id, "putt", 0, "approach");
            var outcomes = new[] { new CourseShotOutcome(.75, ShotLie.Green, 1, false), new(4, ShotLie.Sand, 0, false), new(2, ShotLie.Green, 2, false) };
            active = await service.RecordAsync(active.Id, 0, outcomes[0], "putt");
            active = await service.SelectShotAsync(active.Id, "bunker", 1, "approach");
            active = await service.RecordAsync(active.Id, 1, outcomes[1], "bunker");
            active = await service.RecordAsync(active.Id, 2, outcomes[2], "approach");
            var expected = active.OrderedShots.Select((shot, i) => service.Score(shot, course, outcomes[i])).ToArray();
            CollectionAssert.AreEqual(expected, active.Results.ToArray());
            var round = await service.FinishAsync(active.Id);
            CollectionAssert.AreEqual(new[] { "putt", "bunker", "approach" }, round.CoursePractice!.Course.Shots.Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(expected, round.CoursePractice.Results.ToArray());
            Assert.AreEqual(5, round.Holes[0].FirstPuttDistanceMeters); Assert.AreEqual(expected[0].StrokesGained, round.Holes[0].StrokesGainedPutting);
            Assert.AreEqual(15 / .9144, round.Holes[1].AroundGreenStartDistanceYards); Assert.AreEqual(expected[1].StrokesGained, round.Holes[1].StrokesGainedAroundGreen);
            Assert.AreEqual(110, round.Holes[2].ApproachDistanceMeters, .0001); Assert.AreEqual(expected[2].StrokesGained, round.Holes[2].StrokesGainedApproach);
            AssertShotOrder(active, "putt", "bunker", "approach");
            CollectionAssert.AreEqual(course.Shots.ToArray(), active.Course.Shots.ToArray());
            CollectionAssert.AreEqual(course.Shots.ToArray(), store.Document.Courses[0].Shots.ToArray());
            var export = Path.Combine(directory, "export.json"); await repository.ExportRoundsAsync(export);
            var imported = new FileRoundRepository(Path.Combine(directory, "imported")); await imported.ImportRoundsAsync(export);
            var loaded = (await imported.GetRoundAsync(round.Id))!;
            CollectionAssert.AreEqual(round.CoursePractice.Course.Shots.ToArray(), loaded.CoursePractice!.Course.Shots.ToArray());
            CollectionAssert.AreEqual(expected, loaded.CoursePractice.Results.ToArray());
            CollectionAssert.AreEqual(round.Holes.ToArray(), loaded.Holes.ToArray());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task LegacySchemaOneRoundKeepsOriginalOrderAndWritesSchemaThreeWhenSelected()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var course = SelectableCourse(); var result = Service(new Store()).Score(course.Shots[0], course, new(2, ShotLie.Green, 0, false)) with { ShotId = null };
            var active = new CoursePracticeSession("legacy", DateTime.Now, course, [result], new("3", ShotLie.Green));
            var path = Path.Combine(directory, "course-practice.json");
            await File.WriteAllTextAsync(path, LegacyDocumentJson(1, course, active));
            var store = new FileCoursePracticeRepository(directory); var restored = (await store.LoadAsync()).Active!;
            Assert.IsNull(restored.ShotOrder); AssertShotOrder(restored, "approach", "bunker", "putt"); Assert.AreEqual("3", restored.Input.Distance);
            Assert.AreEqual("bunker", restored.CurrentShot!.Id); Assert.AreEqual(result, restored.Results[0]);
            await Service(store).SelectShotAsync(active.Id, "putt", 1, "bunker");
            var written = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            Assert.AreEqual(3, written["schemaVersion"]!.GetValue<int>());
            CollectionAssert.AreEqual(new[] { "approach", "putt", "bunker" }, written["data"]!["active"]!["shotOrder"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
            Assert.IsNull(written["data"]!["active"]!["orderedShots"]); Assert.IsNull(written["data"]!["active"]!["currentShot"]);
            restored = (await new FileCoursePracticeRepository(directory).LoadAsync()).Active!;
            AssertShotOrder(restored, "approach", "putt", "bunker"); Assert.AreEqual(result with { ShotId = "approach" }, restored.Results[0]);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task InvalidShotPermutationsCannotOverwriteValidPersistedData()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileCoursePracticeRepository(directory); var active = await Service(store).StartAsync(SelectableCourse());
            var document = await store.LoadAsync(); var path = Path.Combine(directory, "course-practice.json");
            var original = await File.ReadAllTextAsync(path); var backup = await File.ReadAllTextAsync(path + ".bak");
            foreach (var invalid in new string[][] { [], ["approach", "bunker"], ["approach", "bunker", "unknown"], ["approach", "approach", "putt"], ["approach", "bunker", null!] })
            {
                await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.SaveAsync(document with { Active = active with { ShotOrder = invalid } }));
                Assert.AreEqual(original, await File.ReadAllTextAsync(path)); Assert.AreEqual(backup, await File.ReadAllTextAsync(path + ".bak"));
            }
            AssertShotOrder((await store.LoadAsync()).Active!, "approach", "bunker", "putt");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task InvalidStoredOrderRecoversValidBackupAndPreservesUnrecoverableFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileCoursePracticeRepository(directory); var service = Service(store); var active = await service.StartAsync(SelectableCourse());
            await service.SelectShotAsync(active.Id, "putt", 0, "approach");
            var path = Path.Combine(directory, "course-practice.json"); var invalid = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            invalid["data"]!["active"]!["shotOrder"] = JsonSerializer.SerializeToNode(new[] { "putt", "putt", "approach" });
            var invalidText = invalid.ToJsonString(); await File.WriteAllTextAsync(path, invalidText);
            AssertShotOrder((await new FileCoursePracticeRepository(directory).LoadAsync()).Active!, "putt", "approach", "bunker");
            await File.WriteAllTextAsync(path + ".bak", invalidText);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.LoadAsync());
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.SaveAsync(CoursePracticeDocument.Empty));
            Assert.AreEqual(invalidText, await File.ReadAllTextAsync(path)); Assert.AreEqual(invalidText, await File.ReadAllTextAsync(path + ".bak"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void AttemptCountsHavePerTaskAndWholeCourseLimits()
    {
        var shot = Course().Shots[0];
        foreach (var attempts in new[] { 0, -1, 101, int.MaxValue })
            Assert.ThrowsExactly<ArgumentException>(() => CoursePracticeService.ValidateCourse(Course(shot with { Attempts = attempts })));
        CoursePracticeService.ValidateCourse(Course(shot with { Attempts = 1 }));
        CoursePracticeService.ValidateCourse(Course(shot with { Attempts = 100 }));
        var thousand = Course(Enumerable.Range(0, 10).Select(i => shot with { Id = $"task-{i}", Attempts = 100 }).ToArray());
        CoursePracticeService.ValidateCourse(thousand);
        Assert.ThrowsExactly<ArgumentException>(() => CoursePracticeService.ValidateCourse(thousand with
        {
            Shots = thousand.Shots.Append(shot with { Id = "extra", Attempts = 1 }).ToArray()
        }));
    }

    [TestMethod]
    public async Task RoundAttemptOverridePersistsWithoutChangingReusableCourseOrFutureRoundDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileCoursePracticeRepository(directory); var service = Service(store); var course = RepeatedCourse();
            await service.SaveCourseAsync(course); var active = await service.StartAsync(course);
            active = await service.SelectShotAsync(active.Id, "bunker", 0, "approach");
            var input = new CourseResultInput("2,5", ShotLie.Sand, 2);
            await service.SaveInputAsync(active.Id, input, 0, "bunker");
            active = await service.UpdateAttemptsAsync(active.Id, "putt", 4, 0, "bunker", 1);
            Assert.AreEqual(8, active.TotalAttempts); Assert.AreEqual("bunker", active.CurrentShot!.Id);
            AssertShotOrder(active, "bunker", "approach", "putt"); Assert.AreEqual(input, active.Input);
            Assert.IsEmpty(active.Results);
            CollectionAssert.AreEqual(course.Shots.Select(s => s.Id == "putt" ? s with { Attempts = 4 } : s).ToArray(), active.Course.Shots.ToArray());
            Assert.AreEqual(course with { Shots = active.Course.Shots }, active.Course);

            var reloadedStore = new FileCoursePracticeRepository(directory); var document = await reloadedStore.LoadAsync();
            var restored = document.Active!;
            Assert.AreEqual(active.Id, restored.Id); Assert.AreEqual(active.Date, restored.Date);
            Assert.AreEqual(8, restored.TotalAttempts); Assert.AreEqual(input, restored.Input);
            CollectionAssert.AreEqual(active.Course.Shots.ToArray(), restored.Course.Shots.ToArray());
            AssertShotOrder(restored, "bunker", "approach", "putt");
            CollectionAssert.AreEqual(course.Shots.ToArray(), document.Courses[0].Shots.ToArray());
            var reloadedService = Service(reloadedStore); await reloadedService.DiscardAsync(restored.Id);
            var next = await reloadedService.StartAsync((await reloadedStore.LoadAsync()).Courses[0]);
            Assert.AreEqual(5, next.TotalAttempts);
            CollectionAssert.AreEqual(new[] { 2, 2, 1 }, next.Course.Shots.Select(s => s.Attempts).ToArray());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task UnplayedPositionCountCanChangeAfterAnotherPositionHasResultsAndPreservesDraft()
    {
        var store = new Store(); var service = Service(store); var course = RepeatedCourse();
        await service.SaveCourseAsync(course); var active = await service.StartAsync(course);
        active = await service.RecordAsync(active.Id, 0, new(3, ShotLie.Green, 1, false), "approach");
        await service.SaveInputAsync(active.Id, new("1,25", ShotLie.Green, 2), 1, "approach");
        var before = store.Document.Active!;
        active = await service.UpdateAttemptsAsync(active.Id, "bunker", 5, 1, "approach", 2);
        Assert.AreEqual(8, active.TotalAttempts); Assert.AreEqual("approach", active.CurrentShot!.Id);
        Assert.AreEqual(2, active.CurrentAttempt); Assert.AreEqual(1, active.CompletedAttempts("approach"));
        Assert.AreEqual(0, active.CompletedAttempts("bunker"));
        Assert.AreSame(before.Results, active.Results); Assert.AreSame(before.Input, active.Input);
        Assert.AreSame(before.ShotOrder, active.ShotOrder); Assert.AreSame(course, store.Document.Courses[0]);
        CollectionAssert.AreEqual(new[] { "approach", "bunker", "putt" }, active.Course.Shots.Select(s => s.Id).ToArray());
        Assert.AreEqual(before.Course with { Shots = active.Course.Shots }, active.Course);
        Assert.AreEqual(before.Course.Shots[1] with { Attempts = 5 }, active.Course.Shots[1]);
        Assert.AreSame(before.Course.Shots[0], active.Course.Shots[0]); Assert.AreSame(before.Course.Shots[2], active.Course.Shots[2]);
    }

    [TestMethod]
    public async Task AttemptCountsCannotChangeForPartlyPlayedOrFinishedPositions()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(RepeatedCourse());
        active = await service.RecordAsync(active.Id, 0, new(2, ShotLie.Green, 0, false), "approach");
        var before = store.Document;
        foreach (var attempts in new[] { 1, 2, 3 })
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.UpdateAttemptsAsync(active.Id, "approach", attempts, 1, "approach", 2));
            Assert.AreSame(before, store.Document);
        }
        active = await service.RecordAsync(active.Id, 1, new(1, ShotLie.Green, 0, false), "approach"); before = store.Document;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.UpdateAttemptsAsync(active.Id, "approach", 3, 2, "bunker", 2));
        Assert.AreSame(before, store.Document); Assert.AreEqual(2, active.CompletedAttempts("approach"));
    }

    [TestMethod]
    public async Task RoundAttemptOverridesEnforcePerPositionRangeWithoutSavingInvalidCounts()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(Course()); var before = store.Document;
        foreach (var invalid in new[] { -1, 0, 101, int.MaxValue })
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.UpdateAttemptsAsync(active.Id, "shot", invalid, 0, "shot", 1));
            Assert.AreSame(before, store.Document);
        }
        active = await service.UpdateAttemptsAsync(active.Id, "shot", 100, 0, "shot", 1);
        Assert.AreEqual(100, active.TotalAttempts);
        active = await service.UpdateAttemptsAsync(active.Id, "shot", 1, 0, "shot", 100);
        Assert.AreEqual(1, active.TotalAttempts);
    }

    [TestMethod]
    public async Task RoundAttemptOverridesEnforceTheThousandAttemptTotalLimit()
    {
        var shot = Course().Shots[0];
        var course = Course(Enumerable.Range(0, 11).Select(i => shot with
        {
            Id = $"task-{i}", Attempts = i == 0 ? 90 : i == 10 ? 10 : 100
        }).ToArray());
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(course); var before = store.Document;
        Assert.AreEqual(1000, active.TotalAttempts);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.UpdateAttemptsAsync(active.Id, "task-0", 91, 0, "task-0", 90));
        Assert.AreSame(before, store.Document);
        active = await service.UpdateAttemptsAsync(active.Id, "task-0", 89, 0, "task-0", 90);
        Assert.AreEqual(999, active.TotalAttempts);
        active = await service.UpdateAttemptsAsync(active.Id, "task-0", 90, 0, "task-0", 89);
        Assert.AreEqual(1000, active.TotalAttempts);
    }

    [TestMethod]
    public async Task AttemptOverridesRejectStaleRoundResultCountCurrentPositionAndExpectedCount()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(RepeatedCourse());
        active = await service.UpdateAttemptsAsync(active.Id, "bunker", 3, 0, "approach", 2);
        active = await service.RecordAsync(active.Id, 0, new(2, ShotLie.Green, 0, false), "approach");
        active = await service.SelectShotAsync(active.Id, "putt", 1, "approach");
        await service.SaveInputAsync(active.Id, new("0,5", ShotLie.Green), 1, "putt"); var before = store.Document;
        foreach (var invalid in new[]
        {
            (Session: "other", Shot: "bunker", Index: 1, Current: (string?)"putt", Attempts: 3),
            (Session: active.Id, Shot: "missing", Index: 1, Current: (string?)"putt", Attempts: 3),
            (Session: active.Id, Shot: "bunker", Index: 0, Current: (string?)"putt", Attempts: 3),
            (Session: active.Id, Shot: "bunker", Index: 2, Current: (string?)"putt", Attempts: 3),
            (Session: active.Id, Shot: "bunker", Index: 1, Current: (string?)"approach", Attempts: 3),
            (Session: active.Id, Shot: "bunker", Index: 1, Current: (string?)null, Attempts: 3),
            (Session: active.Id, Shot: "bunker", Index: 1, Current: (string?)"putt", Attempts: 2)
        })
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.UpdateAttemptsAsync(invalid.Session, invalid.Shot, 4,
                invalid.Index, invalid.Current, invalid.Attempts));
            Assert.AreSame(before, store.Document);
        }
        Assert.AreEqual("0,5", store.Document.Active!.Input.Distance);
        Assert.AreEqual(3, store.Document.Active.Course.Shots.Single(s => s.Id == "bunker").Attempts);
    }

    [TestMethod]
    public async Task CompletedAndDiscardedRoundsRejectAttemptOverrides()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(Course());
        active = await service.RecordAsync(active.Id, 0, new(0, ShotLie.Holed, 0, true), "shot"); var before = store.Document;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.UpdateAttemptsAsync(active.Id, "shot", 2, 1, null, 1));
        Assert.AreSame(before, store.Document); Assert.IsTrue(store.Document.Active!.IsComplete);
        await service.DiscardAsync(active.Id); before = store.Document;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.UpdateAttemptsAsync(active.Id, "shot", 2, 1, null, 1));
        Assert.AreSame(before, store.Document); Assert.IsNull(store.Document.Active);
    }

    [TestMethod]
    public async Task FailedAttemptOverrideKeepsCourseProgressAndDraftAndValidNoOpDoesNotWrite()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(RepeatedCourse());
        await service.SaveInputAsync(active.Id, new("2", ShotLie.Green, 1), 0, "approach"); var before = store.Document;
        store.Failure = new IOException();
        await Assert.ThrowsExactlyAsync<IOException>(() => service.UpdateAttemptsAsync(active.Id, "bunker", 5, 0, "approach", 2));
        Assert.AreSame(before, store.Document); Assert.AreEqual(5, store.Document.Active!.TotalAttempts);
        Assert.AreEqual("2", store.Document.Active.Input.Distance); Assert.IsEmpty(store.Document.Active.Results);
        var unchanged = await service.UpdateAttemptsAsync(active.Id, "bunker", 2, 0, "approach", 2);
        Assert.AreSame(before.Active, unchanged); Assert.AreSame(before, store.Document);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.UpdateAttemptsAsync(active.Id, "bunker", 2, 0, "approach", 1));
        Assert.AreSame(before, store.Document);
    }

    [TestMethod]
    public async Task ChangedRoundCountsDriveRepeatedProgressAndPersistInCompletedArchive()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileCoursePracticeRepository(directory); var rounds = new FileRoundRepository(directory);
            var service = Service(store, rounds); var course = RepeatedCourse(); await service.SaveCourseAsync(course);
            var active = await service.StartAsync(course);
            active = await service.UpdateAttemptsAsync(active.Id, "approach", 3, 0, "approach", 2);
            active = await service.UpdateAttemptsAsync(active.Id, "bunker", 1, 0, "approach", 2);
            active = await service.UpdateAttemptsAsync(active.Id, "putt", 2, 0, "approach", 1);
            active = await service.SelectShotAsync(active.Id, "putt", 0, "approach");
            var expectedIds = new[] { "putt", "putt", "approach", "approach", "approach", "bunker" };
            for (var index = 0; index < expectedIds.Length; index++)
            {
                Assert.IsFalse(active.IsComplete); Assert.AreEqual(6, active.TotalAttempts);
                Assert.AreEqual(expectedIds[index], active.CurrentShot!.Id);
                Assert.AreEqual(expectedIds.Take(index).Count(id => id == expectedIds[index]) + 1, active.CurrentAttempt);
                active = await service.RecordAsync(active.Id, index, new(0, ShotLie.Holed, 0, true), expectedIds[index]);
            }
            Assert.IsTrue(active.IsComplete); Assert.IsNull(active.CurrentShot);
            var restored = (await new FileCoursePracticeRepository(directory).LoadAsync()).Active!;
            Assert.IsTrue(restored.IsComplete); CollectionAssert.AreEqual(expectedIds, restored.PlayedShots.Select(s => s.Id).ToArray());
            var round = await Service(new FileCoursePracticeRepository(directory), rounds).FinishAsync(restored.Id);
            Assert.AreEqual(6, round.ConfiguredHoleCount); Assert.HasCount(6, round.Holes);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, round.CoursePractice!.Course.Shots.Select(s => s.Attempts).ToArray());
            CollectionAssert.AreEqual(expectedIds, round.CoursePractice.PlayedShots.Select(s => s.Id).ToArray());
            var archived = (await new FileRoundRepository(directory).GetRoundAsync(round.Id))!;
            CollectionAssert.AreEqual(round.CoursePractice.Course.Shots.ToArray(), archived.CoursePractice!.Course.Shots.ToArray());
            CollectionAssert.AreEqual(active.Results.ToArray(), archived.CoursePractice.Results.ToArray());
            var document = await new FileCoursePracticeRepository(directory).LoadAsync(); Assert.IsNull(document.Active);
            CollectionAssert.AreEqual(course.Shots.ToArray(), document.Courses[0].Shots.ToArray());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task EachTaskRepeatsUntilItsQuotaBeforeAdvancingAndRoundCompletesAfterAllAttempts()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(RepeatedCourse());
        var expectedIds = new[] { "approach", "approach", "bunker", "bunker", "putt" };
        Assert.AreEqual(5, active.TotalAttempts);
        for (var index = 0; index < expectedIds.Length; index++)
        {
            var shotId = expectedIds[index]; Assert.AreEqual(shotId, active.CurrentShot!.Id);
            Assert.AreEqual(expectedIds.Take(index).Count(id => id == shotId) + 1, active.CurrentAttempt);
            Assert.IsFalse(active.IsComplete);
            active = await service.RecordAsync(active.Id, index, new(0, ShotLie.Holed, 0, true), shotId);
            Assert.AreEqual(shotId, active.Results[^1].ShotId);
            Assert.AreEqual(new CourseResultInput(), active.Input);
        }
        Assert.IsTrue(active.IsComplete); Assert.IsNull(active.CurrentShot); Assert.AreEqual(0, active.CurrentAttempt);
        Assert.AreEqual(2, active.CompletedAttempts("approach")); Assert.AreEqual(2, active.CompletedAttempts("bunker"));
        Assert.AreEqual(1, active.CompletedAttempts("putt"));
        CollectionAssert.AreEqual(expectedIds, active.PlayedShots.Select(s => s.Id).ToArray());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RecordAsync(active.Id, 5, new(0, ShotLie.Holed, 0, true), "putt"));
        active = await service.UndoAsync(active.Id);
        Assert.IsFalse(active.IsComplete); Assert.AreEqual("putt", active.CurrentShot!.Id); Assert.AreEqual(1, active.CurrentAttempt);
        Assert.IsTrue(active.Input.Holed); Assert.AreEqual(0, active.CompletedAttempts("putt"));
    }

    [TestMethod]
    public async Task PartlyPlayedTasksCanBeSelectedAndResumeWithFrozenAttemptCounts()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileCoursePracticeRepository(directory); var service = Service(store); var course = RepeatedCourse();
            await service.SaveCourseAsync(course); var active = await service.StartAsync(course);
            active = await service.RecordAsync(active.Id, 0, new(2, ShotLie.Green, 0, false), "approach");
            active = await service.SelectShotAsync(active.Id, "bunker", 1, "approach");
            active = await service.RecordAsync(active.Id, 1, new(3, ShotLie.Green, 1, false), "bunker");
            active = await service.SelectShotAsync(active.Id, "approach", 2, "bunker");
            await service.SaveInputAsync(active.Id, new("1,5", ShotLie.Green, 2), 2, "approach");
            await service.SaveCourseAsync(course with { Shots = course.Shots.Select(s => s with { Attempts = 10 }).ToArray() });
            var restored = (await new FileCoursePracticeRepository(directory).LoadAsync()).Active!;
            Assert.AreEqual("approach", restored.CurrentShot!.Id); Assert.AreEqual(2, restored.CurrentAttempt); Assert.AreEqual(5, restored.TotalAttempts);
            Assert.AreEqual(1, restored.CompletedAttempts("approach")); Assert.AreEqual(1, restored.CompletedAttempts("bunker"));
            Assert.AreEqual(0, restored.CompletedAttempts("putt")); Assert.AreEqual("1,5", restored.Input.Distance); Assert.AreEqual(2, restored.Input.Penalties);
            CollectionAssert.AreEqual(new[] { "approach", "bunker" }, restored.PlayedShots.Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 2, 2, 1 }, restored.Course.Shots.Select(s => s.Attempts).ToArray());
            Assert.AreEqual(30, (await store.LoadAsync()).Courses[0].Shots.Sum(s => s.Attempts));
            restored = await Service(new FileCoursePracticeRepository(directory)).RecordAsync(restored.Id, 2, new(1.5, ShotLie.Green, 2, false), "approach");
            Assert.AreEqual("bunker", restored.CurrentShot!.Id); Assert.AreEqual(2, restored.CurrentAttempt);
            Assert.HasCount(3, restored.ShotOrder!); Assert.HasCount(3, restored.ShotOrder!.Distinct().ToArray());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task RepeatedAttemptsRejectLateInputResultsAndCompletedTaskSelection()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(RepeatedCourse());
        active = await service.RecordAsync(active.Id, 0, new(2, ShotLie.Green, 0, false), "approach");
        var input = new CourseResultInput("1", ShotLie.Green, 1); await service.SaveInputAsync(active.Id, input, 1, "approach");
        await service.SaveInputAsync(active.Id, new("stale", ShotLie.Sand), 0, "approach");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RecordAsync(active.Id, 0, new(3, ShotLie.Green, 0, false), "approach"));
        Assert.AreEqual(input, store.Document.Active!.Input); Assert.HasCount(1, store.Document.Active.Results);
        active = await service.SelectShotAsync(active.Id, "bunker", 1, "approach");
        await service.SaveInputAsync(active.Id, new("late approach", ShotLie.Green), 1, "approach");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RecordAsync(active.Id, 1, new(1, ShotLie.Green, 0, false), "approach"));
        Assert.AreEqual(new CourseResultInput(), store.Document.Active!.Input); Assert.HasCount(1, store.Document.Active.Results);
        active = await service.SelectShotAsync(active.Id, "approach", 1, "bunker");
        active = await service.RecordAsync(active.Id, 1, new(1, ShotLie.Green, 0, false), "approach");
        var before = store.Document;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.SelectShotAsync(active.Id, "approach", 2, "bunker"));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RecordAsync(active.Id, 2, new(1, ShotLie.Green, 0, false), "approach"));
        Assert.AreSame(before, store.Document); Assert.AreEqual(2, active.CompletedAttempts("approach"));
    }

    [TestMethod]
    public async Task UndoRestoresLastAttemptTaskAfterSwitchingToAnotherPartlyPlayedTask()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(RepeatedCourse());
        active = await service.RecordAsync(active.Id, 0, new(2, ShotLie.Green, 0, false), "approach");
        active = await service.SelectShotAsync(active.Id, "bunker", 1, "approach");
        active = await service.RecordAsync(active.Id, 1, new(4.25, ShotLie.Sand, 2, false), "bunker");
        active = await service.SelectShotAsync(active.Id, "approach", 2, "bunker");
        await service.SaveInputAsync(active.Id, new("other task draft", ShotLie.Green), 2, "approach");
        active = await service.UndoAsync(active.Id);
        Assert.AreEqual("bunker", active.CurrentShot!.Id); Assert.AreEqual(1, active.CurrentAttempt);
        Assert.AreEqual(1, active.CompletedAttempts("approach")); Assert.AreEqual(0, active.CompletedAttempts("bunker"));
        Assert.HasCount(1, active.Results); Assert.AreEqual("approach", active.Results[0].ShotId);
        Assert.AreEqual(4.25.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture), active.Input.Distance);
        Assert.AreEqual(ShotLie.Sand, active.Input.Lie); Assert.AreEqual(2, active.Input.Penalties); Assert.IsFalse(active.Input.Holed);
        active = await service.RecordAsync(active.Id, 1, new(4.25, ShotLie.Sand, 2, false), "bunker");
        Assert.AreEqual("bunker", active.CurrentShot!.Id); Assert.AreEqual(2, active.CurrentAttempt);
        CollectionAssert.AreEqual(new[] { "approach", "bunker" }, active.PlayedShots.Select(s => s.Id).ToArray());
    }

    [TestMethod]
    public async Task FailedRepeatedAttemptSaveAndUndoKeepProgressAndInputIntact()
    {
        var store = new Store(); var service = Service(store); var active = await service.StartAsync(RepeatedCourse());
        active = await service.RecordAsync(active.Id, 0, new(2, ShotLie.Green, 0, false), "approach");
        await service.SaveInputAsync(active.Id, new("1", ShotLie.Green, 1), 1, "approach"); var before = store.Document;
        store.Failure = new IOException();
        await Assert.ThrowsExactlyAsync<IOException>(() => service.RecordAsync(active.Id, 1, new(1, ShotLie.Green, 1, false), "approach"));
        Assert.AreSame(before, store.Document); Assert.AreEqual(1, store.Document.Active!.CompletedAttempts("approach"));
        await Assert.ThrowsExactlyAsync<IOException>(() => service.SelectShotAsync(active.Id, "bunker", 1, "approach"));
        Assert.AreSame(before, store.Document);
        await Assert.ThrowsExactlyAsync<IOException>(() => service.UndoAsync(active.Id)); Assert.AreSame(before, store.Document);
        Assert.AreEqual("1", store.Document.Active!.Input.Distance); Assert.AreEqual(2, store.Document.Active.CurrentAttempt);
        store.Failure = null; active = await service.RecordAsync(active.Id, 1, new(1, ShotLie.Green, 1, false), "approach");
        Assert.AreEqual("bunker", active.CurrentShot!.Id); Assert.AreEqual(2, active.CompletedAttempts("approach"));
    }

    [TestMethod]
    public async Task RepeatedAttemptScoresAndExportedHistoryStayBoundToTasksInActualPlayOrder()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new FileRoundRepository(directory); var service = Service(new Store(), repository); var course = RepeatedCourse();
            var active = await service.StartAsync(course); var ids = new[] { "bunker", "approach", "putt", "approach", "bunker" };
            var outcomes = new[] { new CourseShotOutcome(4, ShotLie.Sand, 0, false), new(3, ShotLie.Green, 1, false),
                new(0, ShotLie.Holed, 0, true), new(1, ShotLie.Green, 0, false), new(2, ShotLie.Green, 2, false) };
            for (var index = 0; index < ids.Length; index++)
            {
                active = await service.SelectShotAsync(active.Id, ids[index], index, active.CurrentShot!.Id);
                active = await service.RecordAsync(active.Id, index, outcomes[index], ids[index]);
            }
            Assert.IsTrue(active.IsComplete); CollectionAssert.AreEqual(ids, active.PlayedShots.Select(s => s.Id).ToArray());
            var expected = active.PlayedShots.Select((shot, i) => service.Score(shot, course, outcomes[i])).ToArray();
            CollectionAssert.AreEqual(expected, active.Results.ToArray());
            Assert.AreEqual(expected[0].ExpectedStart, expected[4].ExpectedStart); Assert.AreEqual(expected[1].ExpectedStart, expected[3].ExpectedStart);
            var round = await service.FinishAsync(active.Id); Assert.AreEqual(5, round.ConfiguredHoleCount); Assert.HasCount(5, round.Holes);
            Assert.HasCount(3, round.CoursePractice!.Course.Shots);
            CollectionAssert.AreEqual(new[] { "bunker", "approach", "putt" }, round.CoursePractice.Course.Shots.Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(ids, round.CoursePractice.PlayedShots.Select(s => s.Id).ToArray());
            Assert.AreEqual(expected[0].StrokesGained, round.Holes[0].StrokesGainedAroundGreen);
            Assert.AreEqual(expected[1].StrokesGained, round.Holes[1].StrokesGainedApproach);
            Assert.AreEqual(expected[2].StrokesGained, round.Holes[2].StrokesGainedPutting);
            Assert.AreEqual(expected[3].StrokesGained, round.Holes[3].StrokesGainedApproach);
            Assert.AreEqual(expected[4].StrokesGained, round.Holes[4].StrokesGainedAroundGreen);
            var export = Path.Combine(directory, "export.json"); await repository.ExportRoundsAsync(export);
            var imported = new FileRoundRepository(Path.Combine(directory, "imported")); await imported.ImportRoundsAsync(export);
            var loaded = (await imported.GetRoundAsync(round.Id))!;
            CollectionAssert.AreEqual(expected, loaded.CoursePractice!.Results.ToArray());
            CollectionAssert.AreEqual(ids, loaded.CoursePractice.ResultShots.Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(round.CoursePractice.Course.Shots.ToArray(), loaded.CoursePractice.Course.Shots.ToArray());
            CollectionAssert.AreEqual(round.Holes.ToArray(), loaded.Holes.ToArray());
            var originalHistory = await File.ReadAllTextAsync(imported.ActiveStoragePath);
            var corruptImport = JsonNode.Parse(await File.ReadAllTextAsync(export))!;
            corruptImport["rounds"]![0]!["coursePractice"]!["results"]![0]!["shotId"] = "unknown";
            var badExport = Path.Combine(directory, "bad-export.json"); await File.WriteAllTextAsync(badExport, corruptImport.ToJsonString());
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => imported.ImportRoundsAsync(badExport));
            Assert.AreEqual(originalHistory, await File.ReadAllTextAsync(imported.ActiveStoragePath));
            await File.WriteAllTextAsync(imported.ActiveStoragePath + ".bak", originalHistory);
            foreach (var invalidShotId in new[] { "unknown", "putt" })
            {
                corruptImport["rounds"]![0]!["coursePractice"]!["results"]![0]!["shotId"] = invalidShotId;
                var corruptHistory = corruptImport.ToJsonString(); await File.WriteAllTextAsync(imported.ActiveStoragePath, corruptHistory);
                var recoveredRepository = new FileRoundRepository(Path.Combine(directory, "imported"));
                var recovered = (await recoveredRepository.GetRoundAsync(round.Id))!;
                Assert.IsTrue(recoveredRepository.WasLastReadRecoveredFromBackup);
                CollectionAssert.AreEqual(expected, recovered.CoursePractice!.Results.ToArray());
                Assert.AreEqual(corruptHistory, await File.ReadAllTextAsync(imported.ActiveStoragePath));
                Assert.AreEqual(originalHistory, await File.ReadAllTextAsync(imported.ActiveStoragePath + ".bak"));
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task LegacySchemaTwoSelectedResultsMigrateToBoundResultsAndDefaultToOneAttempt()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var course = SelectableCourse(); var result = Service(new Store()).Score(course.Shots[2], course, new(1, ShotLie.Green, 1, false)) with { ShotId = null };
            var active = new CoursePracticeSession("legacy-two", DateTime.Now, course, [result], new("3", ShotLie.Green), ["putt", "approach", "bunker"]);
            var path = Path.Combine(directory, "course-practice.json"); await File.WriteAllTextAsync(path, LegacyDocumentJson(2, course, active));
            var store = new FileCoursePracticeRepository(directory); var restored = (await store.LoadAsync()).Active!;
            Assert.AreEqual(3, restored.TotalAttempts); Assert.IsTrue(restored.Course.Shots.All(s => s.Attempts == 1));
            Assert.IsNull(restored.Results[0].ShotId); Assert.AreEqual("putt", restored.PlayedShots[0].Id);
            Assert.AreEqual(1, restored.CompletedAttempts("putt")); Assert.AreEqual("approach", restored.CurrentShot!.Id); Assert.AreEqual("3", restored.Input.Distance);
            restored = await Service(store).SelectShotAsync(active.Id, "bunker", 1, "approach");
            Assert.AreEqual(result with { ShotId = "putt" }, restored.Results[0]); Assert.AreEqual("bunker", restored.CurrentShot!.Id);
            var written = JsonNode.Parse(await File.ReadAllTextAsync(path))!; Assert.AreEqual(3, written["schemaVersion"]!.GetValue<int>());
            Assert.AreEqual("putt", written["data"]!["active"]!["results"]![0]!["shotId"]!.GetValue<string>());
            restored = (await new FileCoursePracticeRepository(directory).LoadAsync()).Active!;
            AssertShotOrder(restored, "putt", "bunker", "approach"); Assert.AreEqual(1, restored.CompletedAttempts("putt"));
            Assert.AreEqual(1, restored.CurrentAttempt); Assert.AreEqual(new CourseResultInput(), restored.Input);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task UnknownOverQuotaAndInvalidMappedResultsCannotOverwriteStoredProgress()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileCoursePracticeRepository(directory); var service = Service(store); var active = await service.StartAsync(RepeatedCourse());
            active = await service.RecordAsync(active.Id, 0, new(2, ShotLie.Green, 0, false), "approach");
            var document = await store.LoadAsync(); var result = active.Results[0]; var path = Path.Combine(directory, "course-practice.json");
            var original = await File.ReadAllTextAsync(path); var backup = await File.ReadAllTextAsync(path + ".bak");
            var invalidResults = new IReadOnlyList<CourseShotResult>[]
            {
                [result with { ShotId = "unknown" }], [result with { ShotId = "APPROACH" }], [result, result, result],
                [result with { ExpectedStart = double.NaN }], [result with { Outcome = new(0, ShotLie.Green, 0, false) }], [null!]
            };
            foreach (var invalid in invalidResults)
            {
                await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.SaveAsync(document with { Active = active with { Results = invalid } }));
                Assert.AreEqual(original, await File.ReadAllTextAsync(path)); Assert.AreEqual(backup, await File.ReadAllTextAsync(path + ".bak"));
            }
            var corrupt = JsonNode.Parse(original)!; corrupt["data"]!["active"]!["results"]![0]!["shotId"] = "missing";
            var corruptText = corrupt.ToJsonString(); await File.WriteAllTextAsync(path, corruptText);
            Assert.AreEqual("approach", (await new FileCoursePracticeRepository(directory).LoadAsync()).Active!.PlayedShots[0].Id);
            await File.WriteAllTextAsync(path + ".bak", corruptText);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.LoadAsync());
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.SaveAsync(CoursePracticeDocument.Empty));
            Assert.AreEqual(corruptText, await File.ReadAllTextAsync(path)); Assert.AreEqual(corruptText, await File.ReadAllTextAsync(path + ".bak"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
