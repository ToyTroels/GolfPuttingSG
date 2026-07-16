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
        var closeGame = await DisplayAlertAsync(
            "Luk spil?",
            "Du er midt i et putting-spil. Vil du lukke spillet og miste den igangværende score?",
            "Luk spil",
            "Bliv her");

        if (closeGame)
        {
            await this.RunNavigationOnceAsync(() => Navigation.PopAsync());
        }
    }

    private async Task NavigateBackAsync()
    {
        await this.RunActionOnceAsync(async () =>
        {
            if (viewModel.IsActive)
            {
                await ConfirmCloseActiveGameAsync();
                return;
            }

            await this.RunNavigationOnceAsync(() => Navigation.PopAsync());
        });
    }
}
