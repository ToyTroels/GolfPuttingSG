using GolfSG.Application.ViewModels;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Courses;
using GolfSG.Application.Accounts;
using GolfSG.Application.Courses;

namespace GolfSG.Views;

public sealed class CoursePracticeResultPage : ContentPage
{
    private CancellationTokenSource? submission;
    public CoursePracticeResultPage(Round round, CourseCommunityWorkflow? community = null)
    {
        var details = round.CoursePractice ?? throw new ArgumentException("Missing course practice details.");
        Title = "Banerunde · Beta"; BackgroundColor = GolfTheme.Colors.PageBackground;
        this.Accessible("CoursePractice.Result", "Resultat fra træningsrunde på banebillede");
        var layout = new VerticalStackLayout { Padding = 16, Spacing = 12 };
        layout.Children.Add(AppViews.PageTitle(details.Course.Name));
        layout.Children.Add(new Label
        {
            Text = round.EndedEarly
                ? $"{round.Date:d} · {details.Results.Count}/{round.ConfiguredHoleCount} slag spillet · Afsluttet tidligt"
                : $"{round.Date:d} · {details.Results.Count} planlagte slag spillet · Beta",
            TextColor = GolfTheme.Colors.Text
        });
        layout.Children.Add(AppViews.PageTitle($"SG {details.Results.Sum(r => r.StrokesGained):+0.00;-0.00;0.00}"));
        layout.Children.Add(new Label { Text = "Hvert slag har sit eget resultat. SG bruger appens omtrentlige referenceværdier. Træningsrunden indgår ikke i statistik for almindelige runder.", TextColor = GolfTheme.Colors.MutedText });
        layout.Children.Add(new Label { Text = details.Course.ScaleNote, TextColor = GolfTheme.Colors.MutedText });
        if (details.Game is { } game && community is not null)
        {
            var message = new Label { Text = "Resultatet er gemt på enheden. Indsend det med forbindelse for at deltage på spillets leaderboard.", TextColor = GolfTheme.Colors.Text };
            var submit = CoursePracticeViews.PrimaryButton("Indsend til spil")
                .Accessible("CourseGame.Result.Submit", "Indsend det gemte resultat til spillets leaderboard");
            var cancel = CoursePracticeViews.SecondaryButton("Annuller ventetid"); cancel.IsVisible = false;
            cancel.Clicked += (_, _) => submission?.Cancel();
            submit.Clicked += async (_, _) =>
            {
                if (submission is not null) return;
                using var cancellation = new CancellationTokenSource(); submission = cancellation;
                submit.IsEnabled = false; cancel.IsVisible = true; message.Text = "Indsender resultat …";
                try
                {
                    var account = await community.SessionAsync(cancellation.Token);
                    await community.SubmitAsync(account, round, cancellation.Token);
                    message.Text = "Resultatet er indsendt. Åbn Mine spil for at se leaderboard.";
                }
                catch (OperationCanceledException) { message.Text = "Indsendelsen blev afbrudt. Dit resultat er gemt; du kan prøve igen."; }
                catch (Exception ex) { message.Text = ex is ArgumentException or InvalidOperationException or ApiAccountException ? ex.Message : "Kunne ikke indsende. Dit resultat er gemt på enheden."; }
                finally { submission = null; submit.IsEnabled = true; cancel.IsVisible = false; }
            };
            var leaderboard = CoursePracticeViews.SecondaryButton("Åbn spil og leaderboard");
            leaderboard.Clicked += async (_, _) => await this.RunNavigationOnceAsync(() => Navigation.PushAsync(
                new CourseGamePage(community, community.Practice, Guid.Parse(game.GameId))));
            // The app's normal history entry can reopen this page and retry a pending submission.
            layout.Children.Add(message); layout.Children.Add(submit); layout.Children.Add(cancel); layout.Children.Add(leaderboard);
        }
        var playedShots = details.PlayedShots;
        var attemptNumbers = new Dictionary<string, int>();
        for (var i = 0; i < details.Results.Count; i++)
        {
            var shot = playedShots[i]; var r = details.Results[i];
            var attemptNumber = attemptNumbers.GetValueOrDefault(shot.Id) + 1; attemptNumbers[shot.Id] = attemptNumber;
            var outcome = r.Outcome.Holed ? "I hul" : $"{r.Outcome.EndDistanceMeters:0.0} m · {ShotLieLabels.Format(r.Outcome.EndLie)}";
            layout.Children.Add(AppViews.Card(new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    AppViews.PageTitle($"{i + 1}. {shot.Name} · slag {attemptNumber} af {shot.Attempts}", 18),
                    new Label { Text = $"Start: {CoursePracticeViews.FormatDistanceMeters(shot.DistanceMeters(details.Course))} · {ShotLieLabels.Format(shot.Lie)}\nSlut: {outcome}\nStrafslag: {r.Outcome.PenaltyStrokes} · SG {r.StrokesGained:+0.00;-0.00;0.00}", TextColor = GolfTheme.Colors.Text }
                }
            }));
        }
        var back = AppViews.PrimaryButton("Tilbage"); back.Clicked += async (_, _) => await this.RunNavigationOnceAsync(() => Navigation.PopAsync());
        layout.Children.Add(back); Content = new ScrollView { Content = layout };
    }
    protected override void OnDisappearing() { submission?.Cancel(); base.OnDisappearing(); }
}
