using GolfSG.Application.Courses;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Courses;

namespace GolfSG.Views;

public sealed class CourseGameSetupPage : CourseCommunityPage
{
    private readonly CoursePracticeService practice;
    private readonly Picker course = new() { Title = "Vælg bane" };
    private readonly Entry name = new() { Placeholder = "Spillets navn", MaxLength = 100 };
    private readonly CourseMapView map = new() { Editable = false, PictureHeight = 350 };
    private readonly Label summary = Text("");
    private readonly Button create;
    private IReadOnlyList<PracticeCourse> courses = [];

    public CourseGameSetupPage(CourseCommunityWorkflow community, CoursePracticeService practice)
        : base(community, "Opret spil", "CourseGame.Create")
    {
        this.practice = practice;
        Body.Children.Add(AppViews.PageTitle("Opret spil"));
        Body.Children.Add(Text("Vælg en gemt bane eller en hentet bibliotekskopi. Alle deltagere spiller samme positioner og antal slag. Banen behøver ikke være offentlig."));
        name.Accessible("CourseGame.Name", "Spillets navn"); course.Accessible("CourseGame.Course", "Vælg bane til spillet");
        Body.Children.Add(name); Body.Children.Add(course); Body.Children.Add(map); Body.Children.Add(summary);
        course.SelectedIndexChanged += (_, _) => Preview();
        create = Action("Opret spil og få kode", "CourseGame.Create.Save", () => RunAsync(async token =>
        {
            if (course.SelectedIndex < 0) throw new ArgumentException("Vælg en bane.");
            if (string.IsNullOrWhiteSpace(name.Text)) throw new ArgumentException("Giv spillet et navn.");
            if (courses[course.SelectedIndex].ImageFile == CoursePracticeImages.SampleFile) await CoursePracticeImages.EnsureSampleAsync();
            var game = await Community.CreateAsync(Session, name.Text.Trim(), courses[course.SelectedIndex], token);
            await Navigation.PushAsync(new CourseGamePage(Community, practice, game.Id)); Navigation.RemovePage(this);
        }));
        Body.Children.Add(create);
    }

    protected override async Task LoadAsync(CancellationToken token)
    {
        courses = (await practice.LoadAsync()).Courses;
        course.ItemsSource = courses.Select(c => c.Name).ToArray();
        course.SelectedIndex = courses.Count > 0 ? 0 : -1;
        create.IsEnabled = courses.Count > 0;
        if (courses.Count == 0) summary.Text = "Gem først en bane under Spil bane, eller hent en fra Banebibliotek.";
        Preview();
    }
    private void Preview()
    {
        if (course.SelectedIndex < 0 || course.SelectedIndex >= courses.Count) { map.IsVisible = false; return; }
        var selected = courses[course.SelectedIndex]; map.IsVisible = true;
        summary.Text = $"{selected.Shots.Count} positioner · {selected.Shots.Sum(s => s.Attempts)} slag · Antallet låses ved oprettelse.";
        map.ImageAspectRatio = selected.ImageAspectRatio; map.SavedShots = selected.Shots; map.Refresh();
        map.SetImage(CoursePracticeImages.PathFor(selected.ImageFile));
    }
}

public sealed class CourseGameJoinPage : CourseCommunityPage
{
    private readonly CoursePracticeService practice;
    private readonly Entry code = new() { Placeholder = "Spilkode, fx H7K2Q9", MaxLength = 6 };
    private readonly VerticalStackLayout preview = new() { Spacing = 10 };
    public CourseGameJoinPage(CourseCommunityWorkflow community, CoursePracticeService practice)
        : base(community, "Deltag med kode", "CourseGame.Join")
    {
        this.practice = practice;
        Body.Children.Add(AppViews.PageTitle("Deltag i et spil"));
        code.Accessible("CourseGame.Code", "Spilkode på seks tegn"); Body.Children.Add(code);
        Body.Children.Add(Action("Find spil", "CourseGame.Find", () => RunAsync(async token =>
        {
            var game = await Community.Api.PreviewGameAsync(Session, code.Text ?? "", token);
            preview.Children.Clear();
            preview.Children.Add(AppViews.PageTitle(game.Name, 20));
            preview.Children.Add(Text($"Vært: {game.HostName} · {game.Course.Name} · {game.Course.Shots.Sum(s => s.Attempts)} slag{(game.Closed ? " · Lukket" : "")}"));
            foreach (var (shot, i) in game.Course.Shots.Select((s, i) => (s, i)))
                preview.Children.Add(Text($"{i + 1}. {shot.Name} · {CoursePracticeViews.FormatDistanceMeters(shot.DistanceMeters(game.Course))} · {shot.Attempts} slag"));
            preview.Children.Add(Text("Når du deltager, deles dit spillernavn, indsendte slagresultater og SG med spillets deltagere. Du spiller i dit eget tempo. Dine andre runder deles ikke."));
            var join = Action("Deltag", "CourseGame.Join.Confirm", () => RunAsync(async ct =>
            {
                await Community.Api.JoinGameAsync(Session, game.Code, ct);
                await Navigation.PushAsync(new CourseGamePage(Community, this.practice, game.Id));
            }));
            join.IsEnabled = !game.Closed; preview.Children.Add(join);
        })));
        Body.Children.Add(preview);
    }
    protected override Task LoadAsync(CancellationToken token) => Task.CompletedTask;
}

public sealed class MyCourseGamesPage : CourseCommunityPage
{
    private readonly CoursePracticeService practice;
    private readonly VerticalStackLayout items = new() { Spacing = 12 };
    private readonly Button more;
    private int offset;
    public MyCourseGamesPage(CourseCommunityWorkflow community, CoursePracticeService practice)
        : base(community, "Mine spil", "CourseGame.Mine")
    {
        this.practice = practice; Body.Children.Add(AppViews.PageTitle("Mine spil")); Body.Children.Add(items);
        more = Action("Vis flere", "CourseGame.Mine.More", () => RunAsync(token => FetchAsync(false, token)));
        more.IsVisible = false; Body.Children.Add(more);
    }
    protected override Task LoadAsync(CancellationToken token) => FetchAsync(true, token);
    private async Task FetchAsync(bool reset, CancellationToken token)
    {
        if (reset) offset = 0;
        var games = await Community.Api.MyGamesAsync(Session, offset, token);
        var savedIds = await Community.SavedRoundIdsAsync(games.Where(g => !g.Submitted && g.RoundId is not null).Select(g => g.RoundId!));
        token.ThrowIfCancellationRequested();
        if (reset) items.Children.Clear();
        if (games.Count == 0 && offset == 0) items.Children.Add(Text("Du har endnu ikke oprettet eller deltaget i et spil."));
        foreach (var entry in games)
        {
            var state = entry.Submitted ? "Resultat indsendt" : entry.RoundId is not null && savedIds.Contains(entry.RoundId) ? "Afventer indsendelse" : "Ikke færdig";
            items.Children.Add(AppViews.Card(new VerticalStackLayout
            {
                Spacing = 8,
                Children = { AppViews.PageTitle(entry.Game.Name, 20), Text($"Kode {entry.Game.Code} · {state}{(entry.Game.Closed ? " · Lukket" : "")}"),
                    Action("Åbn spil", $"CourseGame.Open-{entry.Game.Id}", () => RunLocalAsync(_ => Navigation.PushAsync(new CourseGamePage(Community, practice, entry.Game.Id)))) }
            }));
        }
        offset += games.Count; more.IsVisible = games.Count == CourseCommunityService.PageSize;
    }
}

public sealed class CourseGamePage : CourseCommunityPage
{
    private readonly CoursePracticeService practice;
    private readonly Guid id;
    public CourseGamePage(CourseCommunityWorkflow community, CoursePracticeService practice, Guid id)
        : base(community, "Spil og leaderboard", "CourseGame.Detail") { this.practice = practice; this.id = id; }

    protected override async Task LoadAsync(CancellationToken token)
    {
        var details = await Community.Api.GetGameAsync(Session, id, token);
        var game = details.Game;
        var me = details.Players.FirstOrDefault(p => p.AccountId == Session.AccountId);
        var local = me is null ? null : await Community.FindRoundAsync(me.RoundId);
        Body.Children.Clear(); Body.Children.Add(AppViews.PageTitle(game.Name));
        Body.Children.Add(Text($"{game.Course.Name} · Vært: {game.HostName} · {(game.Closed ? "Lukket" : "Åbent")}"));
        Body.Children.Add(AppViews.PageTitle($"Spilkode: {game.Code}", 22));
        Body.Children.Add(Action("Kopiér kode", "CourseGame.CopyCode", () => RunLocalAsync(async _ => await Clipboard.Default.SetTextAsync(game.Code))));
        Body.Children.Add(Text($"{game.Course.Shots.Count} positioner · {game.Course.Shots.Sum(s => s.Attempts)} slag. Alle spiller samme opsætning i eget tempo."));

        if (!game.Closed && me?.Submitted != true && local is null)
        {
            Func<Func<CancellationToken, Task>, Task> runPlay = me is null ? RunAsync : RunLocalAsync;
            Body.Children.Add(Action(me is null ? "Deltag og spil" : "Spil eller fortsæt", "CourseGame.Play", () => runPlay(async ct =>
            {
                var session = await Community.PlayAsync(Session, game, me, ct);
                await Navigation.PushAsync(new CoursePlayPage(practice, session, Community));
            })));
        }

        if (local is not null)
        {
            Body.Children.Add(Text(me!.Submitted ? "Dit resultat er indsendt." : "Dit resultat er gemt på enheden og afventer indsendelse."));
            Body.Children.Add(Action("Se mit gemte resultat", "CourseGame.LocalResult", () => RunLocalAsync(_ => Navigation.PushAsync(new CoursePracticeResultPage(local, Community)))));
            if (!game.Closed && !me.Submitted)
                Body.Children.Add(Action("Indsend mit resultat", "CourseGame.Submit", () => RunAsync(async ct =>
                { await Community.SubmitAsync(Session, local, ct); await LoadAsync(ct); })));
        }

        if (game.HostId == Session.AccountId && !game.Closed)
            Body.Children.Add(Action("Luk spil", "CourseGame.Close", () => RunAsync(async ct =>
            {
                if (!await DisplayAlertAsync("Luk spil?", "Nye deltagere og resultater kan ikke tilføjes efter lukning. Resultater, som endnu ikke er indsendt, kan stadig gemmes i personlig historik.", "Luk spil", "Tilbage")) return;
                await Community.Api.CloseGameAsync(Session, id, ct); await LoadAsync(ct);
            })));

        Body.Children.Add(AppViews.PageTitle("Leaderboard", 22));
        Body.Children.Add(Text("Færdige, indsendte runder rangeres efter total SG. Højeste score vinder. Ens score med to decimaler deler placering."));
        if (details.Players.Count == 0) Body.Children.Add(Text("Ingen deltagere endnu. Del koden med spillerne."));
        foreach (var player in details.Players)
        {
            var card = new VerticalStackLayout { Spacing = 8 };
            var score = player.TotalStrokesGained is { } total ? $"SG {Math.Round(total, 2, MidpointRounding.AwayFromZero):+0.00;-0.00;0.00}" : "Ikke indsendt";
            card.Children.Add(Text($"{(player.Rank is { } rank ? rank + ". " : "")}{player.Name} · {score}"));
            if (player.Submitted)
            {
                var outcomes = new VerticalStackLayout { Spacing = 6, IsVisible = false };
                var loaded = 0;
                var totalResults = int.MaxValue;
                var detailRows = new VerticalStackLayout { Spacing = 6 };
                async Task LoadResultsAsync(CancellationToken ct)
                {
                    var page = await Community.Api.GetPlayerResultsAsync(Session, id, player.AccountId, loaded, ct);
                    var shots = game.Course.Shots.ToDictionary(s => s.Id);
                    foreach (var (result, i) in page.Results.Select((r, i) => (r, i)))
                        detailRows.Children.Add(Text($"{page.Offset + i + 1}. {shots[result.ShotId!].Name} · {(result.Outcome.Holed ? "I hul" : result.Outcome.EndDistanceMeters.ToString("0.0") + " m")} · Straf {result.Outcome.PenaltyStrokes} · SG {result.StrokesGained:+0.00;-0.00;0.00}"));
                    loaded += page.Results.Count; totalResults = page.Total;
                }
                var more = Action("Vis flere slag", $"CourseGame.MoreOutcomes-{player.AccountId}", () => RunAsync(async ct =>
                {
                    await LoadResultsAsync(ct);
                    outcomes.Children.OfType<Button>().Single().IsVisible = loaded < totalResults;
                }));
                more.IsVisible = false;
                outcomes.Children.Add(detailRows); outcomes.Children.Add(more);
                var toggle = CoursePracticeViews.SecondaryButton("Vis/skjul slagresultater");
                toggle.Accessible($"CourseGame.Outcomes-{player.AccountId}", $"Vis eller skjul slagresultater for {player.Name}");
                toggle.Clicked += async (_, _) =>
                {
                    if (loaded == 0 && !outcomes.IsVisible)
                    {
                        await RunAsync(async ct =>
                        {
                            await LoadResultsAsync(ct); more.IsVisible = loaded < totalResults; outcomes.IsVisible = true;
                        });
                    }
                    else outcomes.IsVisible = !outcomes.IsVisible;
                };
                card.Children.Add(toggle); card.Children.Add(outcomes);
            }
            Body.Children.Add(AppViews.Card(card));
        }
    }
}
