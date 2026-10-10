using GolfSG.Application.Putting;
using GolfSG.Application.Courses;
using GolfSG.Application.Rounds;
using GolfSG.Core;
using GolfSG.Infrastructure.Persistence;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;
using GolfSG.Application.Products;
using GolfSG.Infrastructure.Products;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Infrastructure.Rounds;
using GolfSG.Services;
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

        builder.Services.AddSingleton(_ => new RoundSyncStateStore(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton(serviceProvider => new SyncingRoundRepository(
            new FileRoundRepository(FileSystem.AppDataDirectory), serviceProvider.GetRequiredService<RoundSyncStateStore>()));
        builder.Services.AddSingleton<IRoundRepository>(serviceProvider => serviceProvider.GetRequiredService<SyncingRoundRepository>());
        builder.Services.AddSingleton<IActiveRoundSessionRepository>(_ => new FileActiveRoundSessionRepository(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<IActivePuttingGameRepository>(_ => new FileActivePuttingGameRepository(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<ICoursePracticeRepository>(_ => new FileCoursePracticeRepository(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<CoursePracticeService>();
        builder.Services.AddTransient<CoursePracticeMenuPage>();
        builder.Services.AddTransient<CourseSetupPage>();
        builder.Services.AddTransient<IRoundApplicationService, RoundApplicationService>();
        builder.Services.AddTransient<IPuttingGameSessionService, PuttingGameSessionService>();
        builder.Services.AddSingleton<IDistanceUnitSettings, PreferenceDistanceUnitSettings>();
        builder.Services.AddSingleton<IStrokesGainedPuttingService, StrokesGainedPuttingService>();
        builder.Services.AddSingleton<IStrokesGainedAroundGreenService, StrokesGainedAroundGreenService>();
        builder.Services.AddSingleton<IStrokesGainedApproachService, StrokesGainedApproachService>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<IProductsApiSettings, PreferenceProductsApiSettings>();
        builder.Services.AddSingleton(_ => ProductsHttpClientFactory.Create());
        builder.Services.AddSingleton<IProductsApiService, ProductsApiService>();
        builder.Services.AddSingleton<IRoundsApiService, RoundsApiService>();
        builder.Services.AddSingleton<IRoundSyncService, RoundSyncService>();
        builder.Services.AddTransient<ProductsViewModel>();
        builder.Services.AddTransient<ProductsPage>();
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
