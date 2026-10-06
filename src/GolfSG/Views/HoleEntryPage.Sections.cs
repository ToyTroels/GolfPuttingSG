using GolfSG.Application.ViewModels;
using GolfSG.Application.Services;

namespace GolfSG.Views;

public sealed partial class HoleEntryPage
{
    private View BuildPuttingSection(IReadOnlyList<DistanceQuickPick> puttingQuickPicks)
    {
        if (!viewModel.TrackPutting)
        {
            return new ContentView { IsVisible = false };
        }

        var puttingDistanceValue = DistanceValueLabel();
        puttingDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.FirstPuttDistanceDisplayText));

        var puttingDistanceMinus = DistanceStepperButton("-");
        puttingDistanceMinus.Clicked += (_, _) => viewModel.DecreaseFirstPuttDistance();

        var puttingDistancePlus = DistanceStepperButton("+");
        puttingDistancePlus.Clicked += (_, _) => viewModel.IncreaseFirstPuttDistance();

        var putts = CountLabel();
        putts.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.Putts));

        var puttMinus = RoundStepperButton("-");
        puttMinus.Clicked += (_, _) => viewModel.DecreasePutts();

        var puttPlus = RoundStepperButton("+");
        puttPlus.Clicked += (_, _) => viewModel.IncreasePutts();

        var puttQuickActions = NumberQuickActions(
            nameof(HoleInputViewModel.Putts),
            ("1 putt", 1, () => SetPuttsFromQuickAction(1)),
            ("2 putts", 2, () => SetPuttsFromQuickAction(2)),
            ("3 putts", 3, () => SetPuttsFromQuickAction(3)));

        var puttingSg = SgLabel();
        puttingSg.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.StrokesGainedPuttingText), stringFormat: "SG Putning {0}"));

        var carriedPuttingDistance = CarriedPuttingDistancePanel(() => viewModel.EditCarriedPuttingDistance());

        var puttingSection = PuttingSection(
            puttMinus,
            putts,
            puttPlus,
            puttQuickActions,
            puttingDistanceValue,
            puttingDistanceMinus,
            puttingDistancePlus,
            nameof(HoleInputViewModel.DistanceText),
            puttingQuickPicks,
            nameof(HoleInputViewModel.PuttingDistanceUnitText),
            value => viewModel.FirstPuttDistanceMeters = value,
            carriedPuttingDistance,
            puttingSg);
        var gir = BuildGirChoices();
        var section = new VerticalStackLayout { Spacing = 8, Children = { gir, puttingSection } };
        if (!useGuidedInput)
        {
            section.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsPuttingInputVisible));
        }

        return section;
    }

    private View BuildGirChoices()
    {
        var checkbox = new CheckBox
        {
            Color = PrimaryGreen,
            VerticalOptions = LayoutOptions.Center
        };
        checkbox.SetBinding(CheckBox.IsCheckedProperty, nameof(HoleInputViewModel.IsGirChecked), BindingMode.TwoWay);
        SemanticProperties.SetDescription(checkbox, "Green i regulation (GIR)");
        var label = new Label
        {
            Text = "GIR – green i regulation",
            FontSize = 15,
            TextColor = TextColor,
            VerticalOptions = LayoutOptions.Center
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => viewModel.IsGirChecked = !viewModel.IsGirChecked;
        label.GestureRecognizers.Add(tap);
        return new HorizontalStackLayout
        {
            Spacing = 6,
            Children = { checkbox, label },
            IsVisible = FeatureSettings.RecordGreenInRegulation
        };
    }
    private View BuildApproachSection(IReadOnlyList<DistanceQuickPick> puttingQuickPicks)
    {
        if (!viewModel.TrackApproach)
        {
            return new ContentView { IsVisible = false };
        }

        var approachSg = SgLabel();
        approachSg.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.StrokesGainedApproachText), stringFormat: "SG Indspil {0}"));

        var approachStartLie = QuickChoicePanel(
            "Startleje",
            nameof(HoleInputViewModel.ApproachStartLieText),
            ("Fairway", () => viewModel.SelectApproachStartLie("Fairway")),
            ("Rough", () => viewModel.SelectApproachStartLie("Rough")),
            ("Sand", () => viewModel.SelectApproachStartLie("Sand")),
            ("Problemlie", () => viewModel.SelectApproachStartLie("Problemlie")));

        var approachStartDistance = DistanceSlider(MaxApproachDistanceMeters, SgDistanceInputPresets.ApproachMeters);
        approachStartDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.ApproachStartDistanceYards), BindingMode.TwoWay);

        var approachStartDistanceValue = DistanceValueLabel();
        approachStartDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.ApproachStartDistanceDisplayText));

        var approachStartDistanceMinus = DistanceStepperButton("-");
        approachStartDistanceMinus.Clicked += (_, _) => viewModel.DecreaseApproachStartDistance();

        var approachStartDistancePlus = DistanceStepperButton("+");
        approachStartDistancePlus.Clicked += (_, _) => viewModel.IncreaseApproachStartDistance();

        var approachEndLie = QuickChoicePanel(
            "Slutposition",
            nameof(HoleInputViewModel.ApproachEndLieText),
            ("Green", () => viewModel.SelectApproachEndLie("Green")),
            ("Kortklippet", () => viewModel.SelectApproachEndLie("Kortklippet")),
            ("Rough", () => viewModel.SelectApproachEndLie("Rough")),
            ("Sand", () => viewModel.SelectApproachEndLie("Sand")),
            ("I hul", () => viewModel.SelectApproachEndLie("I hul")));

        var approachEndDistance = DistanceSlider(MaxFinishDistanceMeters, SgDistanceInputPresets.FinishMeters);
        approachEndDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.ApproachEndDistance), BindingMode.TwoWay);

        var approachEndDistanceValue = DistanceValueLabel();
        approachEndDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.ApproachEndDistanceDisplayText));

        var approachEndDistanceMinus = DistanceStepperButton("-");
        approachEndDistanceMinus.Clicked += (_, _) => viewModel.DecreaseApproachEndDistance();

        var approachEndDistancePlus = DistanceStepperButton("+");
        approachEndDistancePlus.Clicked += (_, _) => viewModel.IncreaseApproachEndDistance();

        var approachEndDistancePanel = DistancePanel(
            "Slutafstand",
            approachEndDistance,
            approachEndDistanceValue,
            approachEndDistanceMinus,
            approachEndDistancePlus,
            nameof(HoleInputViewModel.ApproachEndDistanceText),
            [
                new DistanceQuickPickGroup(puttingQuickPicks, nameof(HoleInputViewModel.IsApproachEndOnGreen)),
                new DistanceQuickPickGroup(OffGreenFinishQuickPicks, nameof(HoleInputViewModel.IsApproachEndOffGreen))
            ],
            value => viewModel.ApproachEndDistance = value,
            nameof(HoleInputViewModel.ApproachEndDistanceUnitText));
        approachEndDistancePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsApproachFinishDistanceVisible));

        var approachHoled = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        approachHoled.Accessible(UiAutomationIds.ApproachHoled, "Approachslaget gik i hul");
        approachHoled.SetBinding(Switch.IsToggledProperty, nameof(HoleInputViewModel.ApproachHoled), BindingMode.TwoWay);

        var approachPenaltyStrokes = CountLabel();
        approachPenaltyStrokes.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.ApproachPenaltyStrokes));

        var approachPenaltyMinus = RoundStepperButton("-");
        approachPenaltyMinus.Clicked += (_, _) => viewModel.DecreaseApproachPenaltyStrokes();

        var approachPenaltyPlus = RoundStepperButton("+");
        approachPenaltyPlus.Clicked += (_, _) => viewModel.IncreaseApproachPenaltyStrokes();

        var approachSection = ApproachSection(
            approachStartLie,
            approachStartDistance,
            approachStartDistanceValue,
            approachStartDistanceMinus,
            approachStartDistancePlus,
            nameof(HoleInputViewModel.ApproachStartDistanceText),
            ApproachQuickPicks,
            value => viewModel.ApproachStartDistanceYards = value,
            approachEndLie,
            approachEndDistancePanel,
            approachHoled,
            approachPenaltyMinus,
            approachPenaltyStrokes,
            approachPenaltyPlus,
            approachSg);
        if (!useGuidedInput)
        {
            approachSection.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsApproachInputVisible));
        }

        return approachSection;
    }

    private View BuildAroundGreenSection(IReadOnlyList<DistanceQuickPick> puttingQuickPicks)
    {
        if (!viewModel.TrackAroundGreen)
        {
            return new ContentView { IsVisible = false };
        }

        var aroundGreenStartDistance = DistanceSlider(MaxAroundGreenDistanceMeters, SgDistanceInputPresets.AroundGreenMeters);
        aroundGreenStartDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.AroundGreenStartDistanceYards), BindingMode.TwoWay);

        var aroundGreenStartDistanceValue = DistanceValueLabel();
        aroundGreenStartDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.AroundGreenStartDistanceDisplayText));

        var aroundGreenStartDistanceMinus = DistanceStepperButton("-");
        aroundGreenStartDistanceMinus.Clicked += (_, _) => viewModel.DecreaseAroundGreenStartDistance();

        var aroundGreenStartDistancePlus = DistanceStepperButton("+");
        aroundGreenStartDistancePlus.Clicked += (_, _) => viewModel.IncreaseAroundGreenStartDistance();

        var aroundGreenStartLie = QuickChoicePanel(
            "Startleje",
            nameof(HoleInputViewModel.AroundGreenStartLieText),
            ("Rough", () => viewModel.SelectAroundGreenStartLie("Rough")),
            ("Kortklippet", () => viewModel.SelectAroundGreenStartLie("Kortklippet")),
            ("Sand", () => viewModel.SelectAroundGreenStartLie("Sand")),
            ("Problemlie", () => viewModel.SelectAroundGreenStartLie("Problemlie")));

        var aroundGreenEndLie = QuickChoicePanel(
            "Slutposition",
            nameof(HoleInputViewModel.AroundGreenEndLieText),
            ("Green", () => viewModel.SelectAroundGreenEndLie("Green")),
            ("Kortklippet", () => viewModel.SelectAroundGreenEndLie("Kortklippet")),
            ("Rough", () => viewModel.SelectAroundGreenEndLie("Rough")),
            ("Sand", () => viewModel.SelectAroundGreenEndLie("Sand")),
            ("I hul", () => viewModel.SelectAroundGreenEndLie("I hul")));

        var aroundGreenEndDistance = DistanceSlider(MaxFinishDistanceMeters, SgDistanceInputPresets.FinishMeters);
        aroundGreenEndDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.AroundGreenEndDistance), BindingMode.TwoWay);
        aroundGreenEndDistance.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsAroundGreenFinishDistanceVisible));

        var aroundGreenEndDistanceValue = DistanceValueLabel();
        aroundGreenEndDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.AroundGreenEndDistanceDisplayText));

        var aroundGreenEndDistanceMinus = DistanceStepperButton("-");
        aroundGreenEndDistanceMinus.Clicked += (_, _) => viewModel.DecreaseAroundGreenEndDistance();

        var aroundGreenEndDistancePlus = DistanceStepperButton("+");
        aroundGreenEndDistancePlus.Clicked += (_, _) => viewModel.IncreaseAroundGreenEndDistance();

        var aroundGreenEndDistancePanel = DistancePanel(
            "Slutafstand",
            aroundGreenEndDistance,
            aroundGreenEndDistanceValue,
            aroundGreenEndDistanceMinus,
            aroundGreenEndDistancePlus,
            nameof(HoleInputViewModel.AroundGreenEndDistanceText),
            [
                new DistanceQuickPickGroup(puttingQuickPicks, nameof(HoleInputViewModel.IsAroundGreenEndOnGreen)),
                new DistanceQuickPickGroup(AroundGreenQuickPicks, nameof(HoleInputViewModel.IsAroundGreenEndOffGreen))
            ],
            value => viewModel.AroundGreenEndDistance = value,
            nameof(HoleInputViewModel.AroundGreenEndDistanceUnitText));
        aroundGreenEndDistancePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsAroundGreenFinishDistanceVisible));

        var aroundGreenPenaltyStrokes = CountLabel();
        aroundGreenPenaltyStrokes.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.AroundGreenPenaltyStrokes));

        var aroundGreenPenaltyMinus = RoundStepperButton("-");
        aroundGreenPenaltyMinus.Clicked += (_, _) => viewModel.DecreaseAroundGreenPenaltyStrokes();

        var aroundGreenPenaltyPlus = RoundStepperButton("+");
        aroundGreenPenaltyPlus.Clicked += (_, _) => viewModel.IncreaseAroundGreenPenaltyStrokes();

        var aroundGreenHoled = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        aroundGreenHoled.Accessible(UiAutomationIds.AroundGreenHoled, "Slaget omkring green gik i hul");
        aroundGreenHoled.SetBinding(Switch.IsToggledProperty, nameof(HoleInputViewModel.AroundGreenHoled), BindingMode.TwoWay);

        var aroundGreenSg = SgLabel();
        aroundGreenSg.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.StrokesGainedAroundGreenText), stringFormat: "SG Omkring green {0}"));

        var completedAroundGreenShots = CompletedAroundGreenShotsPanel();

        var addAroundGreenShot = new Button
        {
            Text = "Tilføj nyt slag omkring green",
            HeightRequest = 50,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            FontAttributes = FontAttributes.Bold
        };
        addAroundGreenShot.Accessible(UiAutomationIds.AddAroundGreenShot, "Tilføj endnu et slag omkring green");
        addAroundGreenShot.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.CanAddAnotherAroundGreenShot));
        addAroundGreenShot.Clicked += (_, _) => viewModel.AddAnotherAroundGreenShot();

        var undoAroundGreenShot = new Button
        {
            Text = "Fortryd seneste slag",
            HeightRequest = 44,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = GolfTheme.Colors.DangerText,
            BorderWidth = 1,
            TextColor = GolfTheme.Colors.DangerText,
            FontAttributes = FontAttributes.Bold
        };
        undoAroundGreenShot.Accessible(UiAutomationIds.UndoAroundGreenShot, "Fortryd seneste slag omkring green");
        undoAroundGreenShot.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.CanUndoLastAroundGreenShot));
        undoAroundGreenShot.Clicked += (_, _) => viewModel.UndoLastAroundGreenShot();

        var aroundGreenSection = AroundGreenSection(
            completedAroundGreenShots,
            aroundGreenStartLie,
            aroundGreenStartDistance,
            aroundGreenStartDistanceValue,
            aroundGreenStartDistanceMinus,
            aroundGreenStartDistancePlus,
            nameof(HoleInputViewModel.AroundGreenStartDistanceText),
            AroundGreenQuickPicks,
            value => viewModel.AroundGreenStartDistanceYards = value,
            aroundGreenEndLie,
            aroundGreenEndDistancePanel,
            aroundGreenHoled,
            aroundGreenPenaltyMinus,
            aroundGreenPenaltyStrokes,
            aroundGreenPenaltyPlus,
            addAroundGreenShot,
            undoAroundGreenShot,
            aroundGreenSg);
        if (!useGuidedInput)
        {
            aroundGreenSection.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsAroundGreenInputVisible));
        }

        return aroundGreenSection;
    }

}
