using GolfSG.Application.ViewModels;

namespace GolfSG.Views;

public sealed class StatisticsPage : ContentPage
{
    private readonly StatisticsViewModel viewModel;
    public StatisticsPage(StatisticsViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Statistik (beta)";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        var period = new Picker { Title = "Periode", TextColor = GolfTheme.Colors.Text };
        period.SetBinding(Picker.ItemsSourceProperty, nameof(viewModel.Periods));
        period.SetBinding(Picker.SelectedItemProperty, nameof(viewModel.SelectedPeriod), BindingMode.TwoWay);
        var from = new DatePicker();
        from.SetBinding(DatePicker.DateProperty, nameof(viewModel.StartDate), BindingMode.TwoWay);
        var to = new DatePicker();
        to.SetBinding(DatePicker.DateProperty, nameof(viewModel.EndDate), BindingMode.TwoWay);
        var dates = new VerticalStackLayout { Children = { new Label { Text = "Fra" }, from, new Label { Text = "Til" }, to } };
        dates.SetBinding(IsVisibleProperty, nameof(viewModel.IsCustomPeriod));
        var buckets = new VerticalStackLayout { Spacing = 12 };
        buckets.SetBinding(BindableLayout.ItemsSourceProperty, nameof(viewModel.DistanceBuckets));
        BindableLayout.SetItemTemplate(buckets, new DataTemplate(() => new VerticalStackLayout
        {
            Spacing = 3,
            Children = { Bound(nameof(PuttingDistanceBucketItemViewModel.Name), true), Bound(nameof(PuttingDistanceBucketItemViewModel.StrokesGainedText), true), Bound(nameof(PuttingDistanceBucketItemViewModel.DetailText)) }
        }));
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 16, Spacing = 14,
            Children =
            {
                AppViews.PageTitle("Statistik (beta)", 30),
                new Label { Text = "Runder med putning. Putting-spil er ikke med. Sæsoner følger kalenderåret.", TextColor = GolfTheme.Colors.MutedText },
                AppViews.Card(new VerticalStackLayout { Children = { period, dates } }),
                Bound(nameof(viewModel.SampleText)),
                AppViews.Card(new VerticalStackLayout { Spacing = 8, Children = { new Label { Text = "Putning i perioden", FontAttributes = FontAttributes.Bold }, Bound(nameof(viewModel.SgText), true), Bound(nameof(viewModel.ThreePuttText)) } }),
                AppViews.Card(new VerticalStackLayout { Spacing = 8, Children = { new Label { Text = "Gns. startafstand ved 3-putts", FontAttributes = FontAttributes.Bold }, Bound(nameof(viewModel.ThreePuttDistanceText), true) } }),
                AppViews.Card(new VerticalStackLayout { Spacing = 8, Children = { new Label { Text = "Hvor kan du hente slag?", FontAttributes = FontAttributes.Bold }, Bound(nameof(viewModel.OpportunityText), true), new Label { Text = "Tab mod PGA-reference viser et muligt fokusområde, ikke en garanti for forbedring. Antallet af huller viser datagrundlaget.", FontSize = 12, TextColor = GolfTheme.Colors.MutedText } } }),
                AppViews.Card(new VerticalStackLayout { Spacing = 12, Children = { new Label { Text = "SG efter første putt-afstand", FontAttributes = FontAttributes.Bold }, buckets } })
            }
        } };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await viewModel.LoadAsync(); }
        catch (Exception) { await DisplayAlertAsync("Statistik kunne ikke indlæses", "Prøv igen, eller tjek lagring under Indstillinger.", "OK"); }
    }
    private static Label Bound(string property, bool bold = false)
    {
        var label = new Label { TextColor = GolfTheme.Colors.Text, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None };
        label.SetBinding(Label.TextProperty, property);
        return label;
    }
}
