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
                        "GolfSG gemmer runder, hulinput, putting-spil, benchmarks, banebilleder og indstillinger i appens lokale dataområde. Du kan spille og gemme offline uden en konto. Synkroniser i Historik sender gemte runder til din private konto. Indsend til spil deler kun det valgte spilresultat med spillets deltagere."),
                    Section(
                        "Banebibliotek og spil",
                        "Offentliggør bane sender billede, opsætning og dit spillernavn til serverens bibliotek. Dine resultater offentliggøres ikke. Deltagelse med kode gemmer dit spillernavn og din tilmelding på serveren. Indsend til spil deler dine færdige slagresultater og SG med værten og de øvrige deltagere. Spil og hentede baner kan fortsættes offline. At skjule en bane fjerner den fra biblioteket, men bevarer hentede kopier og eksisterende spil. Sletning af personlig historik sletter ikke et allerede indsendt spilresultat."),
                    Section(
                        "Konto",
                        "Login og kontooprettelse sender brugernavn og adgangskode via HTTPS til den valgte server. Appen gemmer login-tokens i enhedens sikre lager og gemmer aldrig adgangskoden. Serveren gemmer en beskyttet adgangskodehash. Den første konto og server knyttes til denne installations historik. Logout fjerner login-tokens og bevarer historik og tilknytning. Automatisk tokenfornyelse bruges ved serverforespørgsler."),
                    Section(
                        "Valgfri brugsstatistik",
                        "Lokal brugsstatistik er slået fra som standard. Når den aktiveres under Indstillinger, gemmes tællere for knaptryk, færdige huller, rettelser, afstandsinput og sessionsafslutninger. Indtastede værdier og personlige oplysninger gemmes ikke i oversigten. Data sendes ikke automatisk. Du kan stoppe registrering, eksportere eller slette tællerne under Indstillinger."),
                    Section(
                        "Backup og eksport",
                        "Synkroniser i Historik sender gemte runder og henter din kontos runder via HTTPS. Appen forbinder automatisk til GolfSG-serveren. Log ind eller opret en konto via Konto og login på startskærmen. Import fra fil gemmes først lokalt og sendes kun ved manuel synkronisering. Android-cloudbackup er deaktiveret. En eksport indeholder hele rundehistorikken og deles kun, når du selv vælger det."),
                    Section(
                        "Sletning",
                        "Gemte runder har ingen automatisk udløbsdato. Slet individuelle runder i Historik, og synkroniser for også at slette rundens indhold på serveren. Serveren beholder rundens id og versionsnummer for at undgå gendannelse på andre enheder. Lokale gendannelseskopier, eksporter og serverbackups kan stadig indeholde tidligere slettede data. Nulstilling eller afinstallation fjerner lokale data og sender ikke sletninger til serveren."),
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
