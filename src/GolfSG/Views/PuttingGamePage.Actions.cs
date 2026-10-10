namespace GolfSG.Views;

public sealed partial class PuttingGamePage
{
    private async Task SubmitCurrentPuttAsync()
    {
        await this.RunActionOnceAsync(async () =>
        {
            try
            {
                completedRoundId = await viewModel.SubmitAsync();
                if (viewModel.HasError)
                {
                    await DisplayAlertAsync("Resultatet kunne ikke gemmes", viewModel.ErrorMessage, "OK");
                }
            }
            catch (Exception)
            {
                await DisplayAlertAsync(
                    "Resultatet kunne ikke gemmes",
                    "Prøv igen, eller tjek lagring under Indstillinger.",
                    "OK");
            }
        });
    }

    private async Task ConfirmCloseActiveGameAsync()
    {
        var choice = await DisplayActionSheetAsync("Igangværende spil", "Bliv her", null, "Gem og luk", "Kassér spil");
        try
        {
            if (choice == "Gem og luk") await viewModel.FlushAutosaveAsync();
            else if (choice == "Kassér spil")
            {
                if (!await DisplayAlertAsync("Kassér spil?", "Den igangværende score slettes.", "Kassér", "Annuller")) return;
                await viewModel.DiscardAsync();
            }
            else return;
            await this.RunNavigationOnceAsync(() => Navigation.PopAsync());
        }
        catch { await DisplayAlertAsync("Spillet kunne ikke gemmes eller slettes", "Prøv igen. Bliv her, indtil spillet er gemt.", "OK"); }
    }

    private async Task NavigateBackAsync()
    {
        await this.RunActionOnceAsync(async () =>
        {
            if (viewModel.IsBusy) return;
            if (viewModel.IsActive)
            {
                await ConfirmCloseActiveGameAsync();
                return;
            }

            await this.RunNavigationOnceAsync(() => Navigation.PopAsync());
        });
    }
}
