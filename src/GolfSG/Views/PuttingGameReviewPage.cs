using GolfSG.Application.ViewModels;

namespace GolfSG.Views;

public sealed class PuttingGameReviewPage : ContentPage
{
    private readonly PuttingGameViewModel viewModel;

    public PuttingGameReviewPage(PuttingGameViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Ret putt-resultater";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        this.Accessible("practice-review-page", "Registrerede putts");
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            Command = new Command(async () =>
            {
                if (!viewModel.IsBusy) await this.RunNavigationOnceAsync(() => Navigation.PopAsync());
            })
        });

        var results = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            Header = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    AppViews.PageTitle("Registrerede putts"),
                    new Label
                    {
                        Text = "Tryk Ret for at ændre antal putts. Afstande og rækkefølge bevares; score og SG opdateres automatisk.",
                        TextColor = GolfTheme.Colors.MutedText
                    }
                }
            },
            ItemTemplate = new DataTemplate(BuildResultRow)
        };
        results.SetBinding(ItemsView.ItemsSourceProperty, nameof(PuttingGameViewModel.RecordedPutts));
        Content = results;
    }

    protected override bool OnBackButtonPressed() => viewModel.IsBusy || base.OnBackButtonPressed();

    private object BuildResultRow()
    {
        var title = new Label { FontAttributes = FontAttributes.Bold, TextColor = GolfTheme.Colors.Text };
        title.SetBinding(Label.TextProperty, nameof(PracticePuttResult.Title));
        var detail = new Label { TextColor = GolfTheme.Colors.MutedText };
        detail.SetBinding(Label.TextProperty, nameof(PracticePuttResult.Detail));
        var edit = AppViews.SecondaryButton("Ret");
        edit.Accessible("practice-edit-result", "Ret antal putts for dette resultat");
        edit.SetBinding(IsEnabledProperty, new Binding(nameof(PuttingGameViewModel.CanReview), source: viewModel));
        edit.Clicked += async (_, _) =>
        {
            if (edit.BindingContext is PracticePuttResult result) await EditResultAsync(result);
        };
        return AppViews.Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children = { title, detail, edit }
        }, new Thickness(16, 4));
    }

    private async Task EditResultAsync(PracticePuttResult result)
    {
        await this.RunActionOnceAsync(async () =>
        {
            if (!viewModel.CanReview) return;
            var selection = await DisplayActionSheetAsync(
                $"{result.Title} · nu {result.Putts} putts", "Annuller", null,
                "1 putt", "2 putts", "3 putts", "4 putts", "5 putts");
            if (selection is null || selection == "Annuller") return;
            if (!int.TryParse(selection.Split(' ')[0], out var putts) || putts == result.Putts) return;
            await viewModel.EditPuttAsync(result.Index, putts);
            if (viewModel.HasError) await DisplayAlertAsync("Rettelsen kunne ikke gemmes", viewModel.ErrorMessage, "OK");
        });
    }
}
