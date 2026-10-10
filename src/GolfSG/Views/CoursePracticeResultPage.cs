using GolfSG.Application.ViewModels;
using GolfSG.Core.Models;

namespace GolfSG.Views;

public sealed class CoursePracticeResultPage : ContentPage
{
    public CoursePracticeResultPage(Round round)
    {
        var details = round.CoursePractice ?? throw new ArgumentException("Missing course practice details.");
        Title = "Banerunde · Beta"; BackgroundColor = GolfTheme.Colors.PageBackground;
        this.Accessible("CoursePractice.Result", "Resultat fra træningsrunde på banebillede");
        var layout = new VerticalStackLayout { Padding = 16, Spacing = 12 };
        layout.Children.Add(AppViews.PageTitle(details.Course.Name));
        layout.Children.Add(new Label { Text = $"{round.Date:d} · {details.Results.Count} planlagte slag spillet · Beta", TextColor = GolfTheme.Colors.Text });
        layout.Children.Add(AppViews.PageTitle($"SG {details.Results.Sum(r => r.StrokesGained):+0.00;-0.00;0.00}"));
        layout.Children.Add(new Label { Text = "Hvert slag har sit eget resultat. SG bruger appens omtrentlige referenceværdier. Træningsrunden indgår ikke i statistik for almindelige runder.", TextColor = GolfTheme.Colors.MutedText });
        layout.Children.Add(new Label { Text = details.Course.ScaleNote, TextColor = GolfTheme.Colors.MutedText });
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
}
