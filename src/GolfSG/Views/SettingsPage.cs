using System.Reflection;
using GolfSG.Core;
using GolfSG.Application.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class SettingsPage : ContentPage
{
    private static readonly Color PageBackground = GolfTheme.Colors.PageBackground;
    private static readonly Color CardStroke = GolfTheme.Colors.CardStroke;
    private static readonly Color PrimaryGreen = GolfTheme.Colors.PrimaryGreen;
    private static readonly Color TextColor = GolfTheme.Colors.Text;
    private static readonly Color MutedTextColor = GolfTheme.Colors.MutedText;
    private readonly IRoundRepository repository;
    private readonly IDistanceUnitSettings distanceUnitSettings;

    public SettingsPage(IRoundRepository repository, IDistanceUnitSettings distanceUnitSettings)
    {
        this.repository = repository;
        this.distanceUnitSettings = distanceUnitSettings;
        Title = "Indstillinger";
        BackgroundColor = PageBackground;
        this.Accessible(UiAutomationIds.SettingsPage, "Indstillinger");
        BuildLayout();
    }

    private View GirTrackingItem()
    {
        var toggle = new Switch { IsToggled = FeatureSettings.RecordGreenInRegulation, OnColor = PrimaryGreen };
        toggle.Toggled += (_, args) => FeatureSettings.RecordGreenInRegulation = args.Value;
        return AppViews.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = "Registrer GIR ved putning", FontAttributes = FontAttributes.Bold, TextColor = TextColor },
                new Label { Text = "Markér om green blev ramt i regulation: på højst par minus 2 slag. Vises ved hulinput. Gemte GIR-valg bevares, når funktionen slås fra.", FontSize = 13, TextColor = MutedTextColor },
                toggle
            }
        });
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
                        Text = "Indstillinger",
                        FontSize = 30,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor
                    },
                    new Label
                    {
                        Text = "Rundeinput",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    GuidedHoleEntryItem(),
                    GirTrackingItem(),
                    DistanceUnitItem(),
                    new Label
                    {
                        Text = "SG referenceværdier",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    ReferenceItem(
                        "Putting",
                        "Forventede putts pr. første putt-afstand",
                        () => new StrokesGainedReferencePage(
                            "Putting-reference",
                            "Afstand",
                            "Forventede putts",
                            StrokesGainedCalculator.PuttingReference,
                            useDecimalDistance: true)),
                    ReferenceItem(
                        "Indspil",
                        "Forventede slag pr. indspilsafstand",
                        () => new StrokesGainedReferencePage(
                            "Indspilsreference",
                            "Afstand",
                            "Forventede slag",
                            StrokesGainedCalculator.ApproachReference,
                            useDecimalDistance: false)),
                    ReferenceItem(
                        "Omkring green",
                        "Forventede slag pr. afstand og leje",
                        () => new AroundGreenReferencePage(StrokesGainedCalculator.AroundGreenReference)),
                    new Label
                    {
                        Text = "Data",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    ReferenceItem(
                        "Privatliv",
                        "L\u00e6s hvordan GolfSG gemmer og beskytter dine data",
                        () => new PrivacyPage(),
                        UiAutomationIds.PrivacyEntry),
                    ReferenceItem(
                        "Lagring",
                        "Vis aktiv filsti og antal gemte runder",
                        () => new StorageDiagnosticsPage(repository)),
                    new Label
                    {
                        Text = "App",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    VersionInfoItem(),
                    new Label
                    {
                        Text = "Beta",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    BetaFeaturesItem()
                }
            }
        };
    }

    private View GuidedHoleEntryItem()
    {
        var checkbox = new CheckBox
        {
            Color = PrimaryGreen,
            IsChecked = FeatureSettings.UseGuidedHoleEntry,
            VerticalOptions = LayoutOptions.Center
        };

        checkbox.CheckedChanged += (_, args) => FeatureSettings.UseGuidedHoleEntry = args.Value;

        var card = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 12,
                Children =
                {
                    checkbox.Column(0),
                    new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Label
                            {
                                Text = "Guidet hulindtastning",
                                FontSize = 17,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = TextColor
                            },
                            new Label
                            {
                                Text = "Vis approach, omkring green og putting som hurtige trin i stedet for en lang scroll-side.",
                                FontSize = 13,
                                TextColor = MutedTextColor
                            }
                        }
                    }.Column(1)
                }
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => checkbox.IsChecked = !checkbox.IsChecked;
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private View DistanceUnitItem()
    {
        var picker = new Picker
        {
            Title = "Putting-afstande",
            TextColor = TextColor,
            BackgroundColor = Colors.White,
            HeightRequest = 48,
            ItemsSource = new[] { "Meter", "Fod" },
            SelectedIndex = distanceUnitSettings.PuttingDistanceUnit == PuttingDistanceUnitPreference.Feet ? 1 : 0
        };
        picker.Accessible(UiAutomationIds.DistanceUnit, "Vælg enhed for putting-afstande");
        picker.SelectedIndexChanged += (_, _) =>
        {
            distanceUnitSettings.PuttingDistanceUnit = picker.SelectedIndex == 1
                ? PuttingDistanceUnitPreference.Feet
                : PuttingDistanceUnitPreference.Meters;
        };

        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = "Putting-afstande",
                        FontSize = 17,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor
                    },
                    new Label
                    {
                        Text = "Bruges til første putt, green-finish, putting-spil, benchmarks og resultater.",
                        FontSize = 13,
                        TextColor = MutedTextColor
                    },
                    picker
                }
            }
        };
    }

    private static View VersionInfoItem()
    {
        var version = GetReleaseVersionLabel();
        var build = string.IsNullOrWhiteSpace(AppInfo.Current.BuildString)
            ? "ukendt"
            : AppInfo.Current.BuildString;

        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = "Installeret version",
                        FontSize = 17,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor
                    },
                    new Label
                    {
                        Text = $"Version {version} (build {build})",
                        FontSize = 13,
                        TextColor = MutedTextColor
                    }
                }
            }
        };
    }

    private static string GetReleaseVersionLabel()
    {
        var informationalVersion = typeof(SettingsPage).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var version = string.IsNullOrWhiteSpace(informationalVersion)
            ? AppInfo.Current.VersionString
            : informationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            return "ukendt";
        }

        var metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex >= 0 ? version[..metadataIndex] : version;
    }
    private View BetaFeaturesItem()
    {
        var checkbox = new CheckBox
        {
            Color = PrimaryGreen,
            IsChecked = FeatureSettings.EnableBetaFeatures,
            VerticalOptions = LayoutOptions.Center
        };

        checkbox.CheckedChanged += (_, args) => FeatureSettings.EnableBetaFeatures = args.Value;

        var card = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 12,
                Children =
                {
                    checkbox.Column(0),
                    new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Label
                            {
                                Text = "Aktiver beta-funktioner",
                                FontSize = 17,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = TextColor
                            },
                            new Label
                            {
                                Text = "Vis funktioner der stadig er under udvikling.",
                                FontSize = 13,
                                TextColor = MutedTextColor
                            }
                        }
                    }.Column(1)
                }
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => checkbox.IsChecked = !checkbox.IsChecked;
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private View ReferenceItem(
        string title,
        string subtitle,
        Func<Page> createPage,
        string? automationId = null)
    {
        var card = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12,
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Label
                            {
                                Text = title,
                                FontSize = 17,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = TextColor
                            },
                            new Label
                            {
                                Text = subtitle,
                                FontSize = 13,
                                TextColor = MutedTextColor
                            }
                        }
                    }.Column(0),
                    new Label
                    {
                        Text = ">",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = PrimaryGreen,
                        VerticalTextAlignment = TextAlignment.Center
                    }.Column(1)
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(automationId))
        {
            card.Accessible(automationId, title, subtitle);
        }

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await this.RunNavigationOnceAsync(() => Navigation.PushAsync(createPage()));
        card.GestureRecognizers.Add(tap);

        return card;
    }
}
