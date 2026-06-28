using GolfSG.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;

namespace GolfSG.Views;

public sealed class StorageDiagnosticsPage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
    private static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
    private static readonly Color TextColor = Color.FromArgb("#202421");
    private static readonly Color MutedTextColor = Color.FromArgb("#4E5851");

    private readonly IRoundRepository repository;
    private readonly Label savedRounds = new()
    {
        FontSize = 24,
        FontAttributes = FontAttributes.Bold,
        TextColor = TextColor
    };
    private readonly Label storageStatus = new()
    {
        FontSize = 14,
        TextColor = MutedTextColor,
        LineBreakMode = LineBreakMode.WordWrap
    };

    public StorageDiagnosticsPage(IRoundRepository repository)
    {
        this.repository = repository;
        Title = "Lagring";
        BackgroundColor = PageBackground;
        BuildLayout();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var rounds = await repository.GetRoundsAsync();
        savedRounds.Text = rounds.Count.ToString();
        storageStatus.Text = BuildStorageStatus();
    }

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
                    new Label
                    {
                        Text = "Lagring",
                        FontSize = 30,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor
                    },
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
                        ExportAsync),
                    ActionButton(
                        "Importer rounds.json",
                        ImportAsync)
                }
            }
        };
    }

    private async Task ExportAsync()
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

    private async Task ImportAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Vælg rounds.json"
            });

            if (result is null)
            {
                return;
            }

            await repository.ImportRoundsAsync(result.FullPath);
            var rounds = await repository.GetRoundsAsync();
            savedRounds.Text = rounds.Count.ToString();
            storageStatus.Text = BuildStorageStatus();
            await DisplayAlertAsync("Import fuldført", "Rundehistorikken er importeret.", "OK");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await DisplayAlertAsync("Import fejlede", "Den valgte fil kunne ikke importeres.", "OK");
        }
    }

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
                TextColor = MutedTextColor,
                LineBreakMode = lineBreakValue ? LineBreakMode.CharacterWrap : LineBreakMode.WordWrap
            });
    }

    private static View DiagnosticCard(string title, View value)
    {
        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor
                    },
                    value
                }
            }
        };
    }

    private static View ActionButton(string text, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#0F5132"),
            BorderWidth = 1,
            TextColor = Color.FromArgb("#0F5132"),
            CornerRadius = 8
        };
        button.Clicked += async (_, _) => await action();
        return button;
    }
}
