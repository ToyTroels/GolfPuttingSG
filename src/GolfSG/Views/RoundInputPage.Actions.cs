using GolfSG.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Views;

public sealed partial class RoundInputPage
{
    private async Task StartRoundAsync()
    {
        viewModel.StartRound();
        if (viewModel.Holes.FirstOrDefault() is HoleInputViewModel firstHole)
        {
            await this.RunNavigationOnceAsync(
                () => Navigation.PushAsync(new HoleEntryPage(viewModel, firstHole)));
        }
    }

    private async Task SaveRoundAsync(Button saveButton)
    {
        await this.RunActionOnceAsync(async () =>
        {
            saveButton.IsEnabled = false;
            try
            {
                var roundId = await viewModel.SaveAsync();
                if (string.IsNullOrWhiteSpace(roundId))
                {
                    if (viewModel.HasError)
                    {
                        await DisplayAlertAsync("Runden kunne ikke gemmes", viewModel.ErrorMessage, "OK");
                    }

                    return;
                }

                var page = Handler!.MauiContext!.Services.GetRequiredService<RoundResultPage>();
                await page.LoadAsync(roundId);
                await this.RunNavigationOnceAsync(() => Navigation.PushAsync(page));
            }
            catch (Exception)
            {
                await DisplayAlertAsync(
                    "Runden kunne ikke gemmes",
                    "Prøv igen, eller tjek lagring under Indstillinger.",
                    "OK");
            }
            finally
            {
                saveButton.IsEnabled = true;
            }
        });
    }

    private async Task NavigateBackAsync()
    {
        await this.RunActionOnceAsync(async () =>
        {
            if (viewModel.IsRoundVisible)
            {
                await ConfirmCloseRoundAsync();
                return;
            }

            await this.RunNavigationOnceAsync(() => Navigation.PopAsync());
        });
    }

    private async Task ConfirmCloseRoundAsync()
    {
        var closeRound = await DisplayAlertAsync(
            "Luk runde?",
            "Du er midt i en runde. Vil du lukke runden og miste den igangværende score?",
            "Luk runde",
            "Bliv her");

        if (closeRound)
        {
            await this.RunNavigationOnceAsync(() => Navigation.PopToRootAsync());
        }
    }
}
