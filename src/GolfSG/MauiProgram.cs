using GolfSG.Core;
using GolfSG.Services;
using GolfSG.ViewModels;
using GolfSG.Views;
using Microsoft.Extensions.Logging;

namespace GolfSG;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<IRoundRepository, FileRoundRepository>();
        builder.Services.AddSingleton<IStrokesGainedPuttingService, StrokesGainedPuttingService>();
        builder.Services.AddSingleton<IStrokesGainedAroundGreenService, StrokesGainedAroundGreenService>();
        builder.Services.AddSingleton<IStrokesGainedApproachService, StrokesGainedApproachService>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<StartViewModel>();
        builder.Services.AddTransient<RoundInputViewModel>();
        builder.Services.AddTransient<PuttingGameViewModel>();
        builder.Services.AddTransient<RoundResultViewModel>();
        builder.Services.AddTransient<RoundHistoryViewModel>();
        builder.Services.AddTransient<EvaluationViewModel>();
        builder.Services.AddTransient<BenchmarkHistoryViewModel>();
        builder.Services.AddTransient<StartPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<RoundInputPage>();
        builder.Services.AddTransient<PuttingGamesPage>();
        builder.Services.AddTransient<PuttingGamePage>();
        builder.Services.AddTransient<BenchmarkHistoryPage>();
        builder.Services.AddTransient<EvaluationPage>();
        builder.Services.AddTransient<RoundResultPage>();
        builder.Services.AddTransient<RoundHistoryPage>();

        return builder.Build();
    }
}
