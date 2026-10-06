namespace GolfSG.Views;

public sealed class PrivacyPage : ContentPage
{
    public PrivacyPage()
    {
        Title = "Privatliv";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        AutomationId = UiAutomationIds.PrivacyPage;
        SemanticProperties.SetDescription(this, "GolfSG privatlivspolitik");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 14,
                Children =
                {
                    AppViews.PageTitle("Privatliv"),
                    Section(
                        "Data p\u00e5 enheden",
                        "GolfSG gemmer runder, hulinput, putting-spil, benchmarks og indstillinger i appens lokale dataomr\u00e5de. Appen kr\u00e6ver ingen konto og sender ikke disse data til GolfSG eller en analysetjeneste."),
                    Section(
                        "Valgfri brugsstatistik",
                        "Lokal brugsstatistik er slået fra som standard. Når den aktiveres under Indstillinger, gemmes tællere for knaptryk, færdige huller, rettelser, afstandsinput og sessionsafslutninger. Indtastede værdier og personlige oplysninger gemmes ikke i oversigten. Data sendes ikke automatisk. Du kan stoppe registrering, eksportere eller slette tællerne under Indstillinger."),
                    Section(
                        "Backup og eksport",
                        "GolfSG har ingen cloud-synkronisering. Android-cloudbackup er deaktiveret. Operativsystemets enhedsbackup eller enhedsoverf\u00f8rsel kan stadig h\u00e5ndteres af platformen. En eksport indeholder hele rundehistorikken og deles kun, n\u00e5r du selv v\u00e6lger det."),
                    Section(
                        "Sletning",
                        "Gemte runder har ingen automatisk udl\u00f8bsdato. Slet individuelle runder i Historik. Lokale gendannelseskopier og cachelagrede eksporter kan stadig indeholde tidligere slettede data. Du kan fjerne alle lokale GolfSG-data via enhedens nulstil appdata- eller afinstallationsfunktion. Eksporterede kopier skal slettes separat."),
                    Section(
                        "Kontakt",
                        "Privatlivssp\u00f8rgsm\u00e5l og fejl kan rapporteres i projektets offentlige GitHub-repository."),
                    RepositoryButton()
                }
            }
        };
    }

    private static Border Section(string title, string body) =>
        AppViews.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 17,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = GolfTheme.Colors.Text
                },
                new Label
                {
                    Text = body,
                    FontSize = 14,
                    TextColor = GolfTheme.Colors.MutedText,
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        });

    private static Button RepositoryButton()
    {
        var button = AppViews.SecondaryButton("\u00c5bn fuld privatlivspolitik");
        button.AutomationId = UiAutomationIds.PrivacyPolicyLink;
        SemanticProperties.SetDescription(button, "\u00c5bn GolfSGs fulde privatlivspolitik i browseren");
        button.Clicked += async (_, _) =>
            await Launcher.Default.OpenAsync("https://toytroels.github.io/GolfPuttingSG/privacy.html");
        return button;
    }
}
