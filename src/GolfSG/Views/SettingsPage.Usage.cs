using GolfSG.Application.Services;

namespace GolfSG.Views;

public sealed partial class SettingsPage
{
    private View UsageDiagnosticsItem()
    {
        var toggle = new Switch { IsToggled = UsageDiagnostics.Enabled, OnColor = PrimaryGreen };
        toggle.Toggled += (_, args) => UsageDiagnostics.Enabled = args.Value;
        var export = AppViews.SecondaryButton("Eksportér brugsoversigt");
        export.Clicked += async (_, _) => await this.RunActionOnceAsync(async () =>
        {
            try { await UsageDiagnostics.ExportAsync(); }
            catch { await DisplayAlertAsync("Eksport fejlede", "Brugsoversigten kunne ikke eksporteres. Prøv igen.", "OK"); }
        });
        var clear = AppViews.SecondaryButton("Slet brugsoversigt");
        clear.Clicked += async (_, _) => await this.RunActionOnceAsync(async () =>
        {
            if (await DisplayAlertAsync("Slet brugsoversigt?", "Rundedata bliver bevaret.", "Slet", "Annuller"))
            {
                try { await UsageDiagnostics.ClearAsync(); }
                catch { await DisplayAlertAsync("Sletning fejlede", "Brugsoversigten kunne ikke slettes. Prøv igen.", "OK"); }
            }
        });
        return AppViews.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Lokal brugsstatistik (valgfri)", FontAttributes = FontAttributes.Bold, TextColor = TextColor },
                new Label
                {
                    Text = "Registrér knaptryk pr. færdigt hul, rettelser og valg af afstandsinput. Kun lokale tællere; ingen indtastede værdier, navne eller rundedata. Intet sendes automatisk. Slå fra for at stoppe registrering; eksisterende tællere kan eksporteres eller slettes.",
                    FontSize = 13, TextColor = MutedTextColor
                },
                toggle,
                new Label
                {
                    Text = "Uventede sessionsafslutninger registreres også. De kan skyldes nedbrud eller at Android lukker appen og er ikke en præcis nedbrudsrate.",
                    FontSize = 13, TextColor = MutedTextColor
                },
                export, clear
            }
        });
    }
}
