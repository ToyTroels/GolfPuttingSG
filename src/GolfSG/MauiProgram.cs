using GolfSG.Application.Putting;
using GolfSG.Application.Rounds;
using GolfSG.Core;
using GolfSG.Infrastructure.Persistence;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;
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

        builder.Services.AddSingleton<IRoundRepository>(_ => new FileRoundRepository(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<IActiveRoundSessionRepository>(_ => new FileActiveRoundSessionRepository(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<IActivePuttingGameRepository>(_ => new FileActivePuttingGameRepository(FileSystem.AppDataDirectory));
        builder.Services.AddTransient<IRoundApplicationService, RoundApplicationService>();
        builder.Services.AddTransient<IPuttingGameSessionService, PuttingGameSessionService>();
        builder.Services.AddSingleton<IDistanceUnitSettings, PreferenceDistanceUnitSettings>();
        builder.Services.AddSingleton<IStrokesGainedPuttingService, StrokesGainedPuttingService>();
        builder.Services.AddSingleton<IStrokesGainedAroundGreenService, StrokesGainedAroundGreenService>();
        builder.Services.AddSingleton<IStrokesGainedApproachService, StrokesGainedApproachService>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<StartViewModel>();
        builder.Services.AddTransient(serviceProvider => new RoundInputViewModel(
            serviceProvider.GetRequiredService<IRoundApplicationService>(),
            serviceProvider.GetRequiredService<IDistanceUnitSettings>(),
            serviceProvider.GetRequiredService<IActiveRoundSessionRepository>()));
        builder.Services.AddTransient(serviceProvider => new PuttingGameViewModel(
            serviceProvider.GetRequiredService<IPuttingGameSessionService>(),
            serviceProvider.GetRequiredService<IDistanceUnitSettings>(),
            serviceProvider.GetRequiredService<IActivePuttingGameRepository>()));
        builder.Services.AddTransient<RoundResultViewModel>();
        builder.Services.AddTransient<RoundHistoryViewModel>();
        builder.Services.AddTransient<StatisticsViewModel>();
        builder.Services.AddTransient<StatisticsPage>();
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
