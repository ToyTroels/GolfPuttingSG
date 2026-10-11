using GolfSG.Application.Services;
using GolfSG.Infrastructure.Persistence;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace GolfSG.Views;

public sealed class StorageDiagnosticsPage : ContentPage
{
    private readonly IRoundRepository repository;
    private CancellationTokenSource? pageCancellation;
    private readonly Label savedRounds = new()
    {
        FontSize = 24,
        FontAttributes = FontAttributes.Bold,
        TextColor = GolfTheme.Colors.Text
    };
    private readonly Label storageStatus = new()
    {
        FontSize = 14,
        TextColor = GolfTheme.Colors.MutedText,
        LineBreakMode = LineBreakMode.WordWrap
    };

    public StorageDiagnosticsPage(IRoundRepository repository)
    {
        this.repository = repository;
        Title = "Lagring";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        BuildLayout();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        pageCancellation?.Cancel();
        pageCancellation?.Dispose();
        pageCancellation = new();
        var rounds = await repository.GetRoundsAsync();
        savedRounds.Text = rounds.Count.ToString();
        storageStatus.Text = BuildStorageStatus();
    }

    protected override void OnDisappearing()
    {
        pageCancellation?.Cancel();
        base.OnDisappearing();
    }

    private CancellationToken PageToken => pageCancellation?.Token ?? new CancellationToken(canceled: true);

    private void BuildLayout()
    {
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 14,
                Children =
                {
                    AppViews.PageTitle("Lagring"),
                    DiagnosticCard(
                        "Aktiv fil",
                        repository.ActiveStoragePath,
                        lineBreakValue: true),
                    DiagnosticCard(
                        "Gemte runder",
                        savedRounds),
                    DiagnosticCard(
                        "Status",
                        storageStatus),
                    ActionButton(
                        "Eksporter rounds.json",
                        ExportAsync,
                        UiAutomationIds.StorageExport),
                    ActionButton(
                        "Importer rounds.json",
                        ImportAsync,
                        UiAutomationIds.StorageImport)
                }
            }
        };
    }

    private async Task ExportAsync() =>
        await this.RunActionOnceAsync(ExportCoreAsync);

    private async Task ExportCoreAsync()
    {
        try
        {
            var exportPath = System.IO.Path.Combine(FileSystem.CacheDirectory, "GolfSG-rounds.json");
            await repository.ExportRoundsAsync(exportPath);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Eksporter rounds.json",
                File = new ShareFile(exportPath)
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await DisplayAlertAsync("Eksport fejlede", "Rundehistorikken kunne ikke eksporteres.", "OK");
        }
    }

    private Task ImportAsync() =>
        RoundImportActions.ImportAsync(this, new RoundImportService(repository, FileSystem.CacheDirectory), async () =>
        {
            var rounds = await repository.GetRoundsAsync();
            savedRounds.Text = rounds.Count.ToString();
            storageStatus.Text = BuildStorageStatus();
        }, PageToken);

    private string BuildStorageStatus()
    {
        if (repository.WasLastReadMigratedFromLegacyStorage)
        {
            return $"Migreret fra: {repository.LastMigrationSourcePath}";
        }

        if (repository.WasLastReadRecoveredFromBackup)
        {
            return "Rundehistorikken blev gendannet fra backup.";
        }

        if (repository.WasLastUnreadableActiveFilePreserved)
        {
            return $"En ulæselig fil blev bevaret: {repository.LastPreservedUnreadableFilePath}";
        }

        return "Ingen problemer fundet.";
    }

    private static View DiagnosticCard(string title, string value, bool lineBreakValue = false)
    {
        return DiagnosticCard(
            title,
            new Label
            {
                Text = value,
                FontSize = 14,
                TextColor = GolfTheme.Colors.MutedText,
                LineBreakMode = lineBreakValue ? LineBreakMode.CharacterWrap : LineBreakMode.WordWrap
            });
    }

    private static View DiagnosticCard(string title, View value)
    {
        return AppViews.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = GolfTheme.Colors.Text
                },
                value
            }
        });
    }

    private static View ActionButton(string text, Func<Task> action, string automationId)
    {
        var button = AppViews.SecondaryButton(text).Accessible(automationId, text);
        button.Clicked += async (_, _) => await action();
        return button;
    }
}
