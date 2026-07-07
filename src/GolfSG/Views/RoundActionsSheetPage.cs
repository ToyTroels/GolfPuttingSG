using GolfSG.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class RoundActionsSheetPage : ContentPage
{
    private static readonly Color SheetBackground = Colors.White;
    private static readonly Color ScrimColor = GolfTheme.Colors.Scrim;
    private static readonly Color TextColor = GolfTheme.Colors.Text;
    private static readonly Color MutedTextColor = GolfTheme.Colors.MutedText;
    private static readonly Color DangerRed = GolfTheme.Colors.DangerText;
    private static readonly Color DividerColor = GolfTheme.Colors.Divider;

    private readonly RoundListItemViewModel round;
    private readonly Func<RoundListItemViewModel, Task> deleteRoundAsync;

    public RoundActionsSheetPage(
        RoundListItemViewModel round,
        Func<RoundListItemViewModel, Task> deleteRoundAsync)
    {
        this.round = round;
        this.deleteRoundAsync = deleteRoundAsync;
        BackgroundColor = Colors.Transparent;
        BuildLayout();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    private void BuildLayout()
    {
        var dismissArea = new BoxView
        {
            BackgroundColor = ScrimColor
        };
        var dismissTap = new TapGestureRecognizer();
        dismissTap.Tapped += async (_, _) => await CloseAsync();
        dismissArea.GestureRecognizers.Add(dismissTap);

        var dragHandle = new BoxView
        {
            WidthRequest = 42,
            HeightRequest = 4,
            CornerRadius = 2,
            BackgroundColor = GolfTheme.Colors.SheetHandle,
            HorizontalOptions = LayoutOptions.Center
        };

        var title = new Label
        {
            Text = "Runde",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor
        };

        var date = new Label
        {
            Text = round.Date,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor
        };

        var detail = new Label
        {
            Text = round.DetailText,
            FontSize = 14,
            TextColor = MutedTextColor
        };

        var sg = new Label
        {
            Text = round.TotalSgText,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = GolfTheme.Colors.PrimaryGreen,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        };

        var roundSummary = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { date, detail }
                }.Column(0),
                sg.Column(1)
            }
        };

        var deleteButton = new Button
        {
            Text = "Slet runde",
            BackgroundColor = GolfTheme.Colors.DangerBackground,
            BorderColor = DangerRed,
            BorderWidth = 1,
            TextColor = DangerRed,
            CornerRadius = 8,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 46
        };
        deleteButton.Clicked += async (_, _) =>
        {
            await CloseAsync();
            await deleteRoundAsync(round);
        };

        var cancelButton = new Button
        {
            Text = "Annuller",
            BackgroundColor = Colors.White,
            BorderColor = DividerColor,
            BorderWidth = 1,
            TextColor = TextColor,
            CornerRadius = 8,
            HeightRequest = 44
        };
        cancelButton.Clicked += async (_, _) => await CloseAsync();

        var sheet = new Border
        {
            BackgroundColor = SheetBackground,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(18, 18, 0, 0)
            },
            Padding = new Thickness(20, 10, 20, 22),
            VerticalOptions = LayoutOptions.End,
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    dragHandle,
                    title,
                    roundSummary,
                    new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = DividerColor
                    },
                    deleteButton,
                    cancelButton
                }
            }
        };

        Content = new Grid
        {
            Children =
            {
                dismissArea,
                sheet
            }
        };
    }

    private async Task CloseAsync()
    {
        if (Navigation.ModalStack.Contains(this))
        {
            await Navigation.PopModalAsync(false);
        }
    }
}
