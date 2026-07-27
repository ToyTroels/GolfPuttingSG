using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;

namespace GolfSG.Application.ViewModels;

public sealed partial class HoleInputViewModel
{
    private void Recalculate()
    {
        var hole = ToHole();
        ExpectedPutts = hole.ExpectedPutts;
        ExpectedApproachShots = hole.ExpectedApproachShots;
        ExpectedApproachFinishStrokes = hole.ExpectedApproachFinishStrokes;
        ExpectedAroundGreenStartStrokes = hole.ExpectedAroundGreenStartStrokes;
        ExpectedAroundGreenFinishStrokes = hole.ExpectedAroundGreenFinishStrokes;
        StrokesGainedPutting = hole.StrokesGainedPutting;
        StrokesGainedApproach = hole.StrokesGainedApproach;
        StrokesGainedAroundGreen = hole.StrokesGainedAroundGreen;
        OnPropertyChanged(nameof(StrokesGainedText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
    }

    private void OnTrackingChanged()
    {
        Recalculate();
        OnFlowVisibilityChanged();
        OnPropertyChanged(nameof(StrokesGainedText));
        OnPropertyChanged(nameof(DetailText));
    }

    private void OnFlowVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsApproachInputVisible));
        OnPropertyChanged(nameof(IsAroundGreenInputVisible));
        OnPropertyChanged(nameof(IsPuttingInputVisible));
        NotifyPuttingDistanceCarryForwardChanged();
        OnPropertyChanged(nameof(CanAdvanceApproachStep));
        OnPropertyChanged(nameof(CanAdvanceAroundGreenStep));
        OnPropertyChanged(nameof(CanAdvancePuttingStep));
        OnPropertyChanged(nameof(IsApproachEndOnGreen));
        OnPropertyChanged(nameof(IsApproachEndOffGreen));
        OnPropertyChanged(nameof(IsAroundGreenEndOnGreen));
        OnPropertyChanged(nameof(IsAroundGreenEndOffGreen));
        OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
    }

    private string FormatPuttDistance()
    {
        var distance = ParsePuttingDistance(DistanceText);
        return distance > 0 ? UiFormat.PuttingDistance(distance, PuttingDistanceUnit) : "-";
    }

    private string FormatApproachDistance()
    {
        var distance = ParseDistance(ApproachDistanceText);
        return distance > 0 ? UiFormat.Meters(distance) : "-";
    }

    private string FormatApproachStartDistance()
    {
        var distance = ParseDistance(ApproachStartDistanceText);
        return distance > 0 ? UiFormat.Meters(distance) : "-";
    }

    private string FormatApproachEndDistance()
    {
        if (ApproachHoled)
        {
            return "I hul";
        }

        var distance = ParseFinishDistance(ApproachEndDistanceText, ParseLie(ApproachEndLieText));
        if (distance <= 0)
        {
            return "-";
        }

        return ParseLie(ApproachEndLieText) == ShotLie.Green
            ? UiFormat.PuttingDistance(distance, PuttingDistanceUnit)
            : UiFormat.Meters(distance);
    }

    private string FormatApproachEndDistanceToGreenEdge()
    {
        var distance = ParseDistance(ApproachEndDistanceToGreenEdgeText);
        return distance > 0 ? UiFormat.Meters(distance) : "-";
    }

    private GolfShot BuildApproachShot()
    {
        var endLie = ParseLie(ApproachEndLieText);
        return ShotInputMapper.BuildApproachShot(new ApproachShotInput(
            HoleNumber,
            ApproachPar,
            ApproachIsTeeShot,
            ParseDistance(ApproachStartDistanceText),
            ParseLie(ApproachStartLieText),
            ParseFinishDistance(ApproachEndDistanceText, endLie),
            endLie,
            ApproachPenaltyStrokes,
            ApproachHoled,
            ParseDistance(ApproachEndDistanceToGreenEdgeText)));
    }

    private string FormatAroundGreenStartDistance()
    {
        var distance = ParseDistance(AroundGreenStartDistanceText);
        return distance > 0 ? UiFormat.WholeMeters(distance) : "-";
    }

    private string FormatAroundGreenEndDistance()
    {
        if (AroundGreenHoled)
        {
            return "I hul";
        }

        var endLie = ParseLie(AroundGreenEndLieText);
        var distance = ParseFinishDistance(AroundGreenEndDistanceText, endLie);
        if (distance <= 0)
        {
            return "-";
        }

        return endLie == ShotLie.Green
            ? UiFormat.PuttingDistance(distance, PuttingDistanceUnit)
            : UiFormat.Meters(distance);
    }

    private IReadOnlyList<GolfShot> BuildAroundGreenShots()
    {
        if (!IsAroundGreenInputVisible)
        {
            return [];
        }

        var shots = completedAroundGreenShots.ToList();
        var currentShot = BuildAroundGreenShot(shots.Count + 1);
        if (currentShot.StartDistanceToPin > 0)
        {
            shots.Add(currentShot);
        }

        return shots;
    }

    private GolfShot BuildAroundGreenShot(int shotNumber)
    {
        var endLie = ParseLie(AroundGreenEndLieText);
        return ShotInputMapper.BuildAroundGreenShot(new AroundGreenShotInput(
            HoleNumber,
            shotNumber,
            ParseDistance(AroundGreenStartDistanceText),
            ParseLie(AroundGreenStartLieText),
            ParseFinishDistance(AroundGreenEndDistanceText, endLie),
            endLie,
            AroundGreenPenaltyStrokes,
            AroundGreenHoled));
    }

    private bool ApproachFinishedHoled => TrackApproach &&
        (ApproachHoled || ParseLie(ApproachEndLieText) == ShotLie.Holed);

    private bool ApproachFinishedOnGreenOrHoled => TrackApproach &&
        (ApproachFinishedHoled || ParseLie(ApproachEndLieText) == ShotLie.Green);

    private bool AroundGreenFinishedHoled => IsAroundGreenInputVisible &&
        (AroundGreenHoled || ParseLie(AroundGreenEndLieText) == ShotLie.Holed);

    private void ApplyAroundGreenCarryForward()
    {
        var result = HoleCarryForwardService.FromAroundGreen(new AroundGreenCarryForwardRequest(
            TrackAroundGreen,
            TrackPutting,
            AroundGreenFinishedHoled,
            ParseFinishDistance(AroundGreenEndDistanceText, ParseLie(AroundGreenEndLieText)),
            ParseLie(AroundGreenEndLieText),
            hasManualPuttingDistanceOverride,
            MaxFirstPuttDistanceMeters));

        if (result.PuttingSource != HoleCarryForwardPuttingSource.AroundGreen)
        {
            ClearCarriedPuttingDistance(HoleCarryForwardPuttingSource.AroundGreen);
        }

        ApplyCarryForwardResult(result);
    }

    private void ApplyApproachCarryForward()
    {
        var result = HoleCarryForwardService.FromApproach(new ApproachCarryForwardRequest(
            TrackApproach,
            TrackPutting,
            TrackAroundGreen,
            ApproachFinishedHoled,
            ParseFinishDistance(ApproachEndDistanceText, ParseLie(ApproachEndLieText)),
            ParseLie(ApproachEndLieText),
            hasManualPuttingDistanceOverride,
            ParseDistance(AroundGreenStartDistanceText),
            carriedAroundGreenStartDistanceFromApproachMeters,
            AroundGreenStartLieText,
            carriedAroundGreenStartLieFromApproach,
            MaxFirstPuttDistanceMeters,
            MaxAroundGreenDistanceMeters));

        if (result.PuttingSource != HoleCarryForwardPuttingSource.Approach)
        {
            ClearCarriedPuttingDistance(HoleCarryForwardPuttingSource.Approach);
        }

        ApplyCarryForwardResult(result);
    }

    private void ApplyCarryForwardResult(HoleCarryForwardResult result)
    {
        if (result == HoleCarryForwardResult.Empty)
        {
            return;
        }

        ApplyApproachCarryForwardValue(() =>
        {
            if (result.PuttingDistanceMeters is not null)
            {
                FirstPuttDistanceMeters = result.PuttingDistanceMeters.Value;
                if (result.PuttingSource == HoleCarryForwardPuttingSource.Approach)
                {
                    carriedPuttingDistanceFromApproachMeters = ParsePuttingDistance(DistanceText);
                }
                else if (result.PuttingSource == HoleCarryForwardPuttingSource.AroundGreen)
                {
                    carriedPuttingDistanceFromAroundGreenMeters = ParsePuttingDistance(DistanceText);
                    carriedPuttingDistanceFromApproachMeters = null;
                }
            }

            if (result.AroundGreenStartDistanceMeters is not null)
            {
                AroundGreenStartDistanceYards = result.AroundGreenStartDistanceMeters.Value;
                carriedAroundGreenStartDistanceFromApproachMeters = ParseDistance(AroundGreenStartDistanceText);
            }

            if (!string.IsNullOrWhiteSpace(result.AroundGreenStartLieText))
            {
                AroundGreenStartLieText = result.AroundGreenStartLieText;
                carriedAroundGreenStartLieFromApproach = result.AroundGreenStartLieText;
            }
        });

        NotifyPuttingDistanceCarryForwardChanged();
    }

    private void ApplyApproachCarryForwardValue(Action apply)
    {
        isApplyingApproachCarryForward = true;
        try
        {
            apply();
        }
        finally
        {
            isApplyingApproachCarryForward = false;
        }
    }

    private void NotifyPuttingDistanceCarryForwardChanged()
    {
        OnPropertyChanged(nameof(HasCarriedPuttingDistance));
        OnPropertyChanged(nameof(IsPuttingDistanceInputVisible));
        OnPropertyChanged(nameof(CarriedPuttingDistanceText));
    }

    private void ClearCarriedPuttingDistance(HoleCarryForwardPuttingSource source)
    {
        var changed = false;
        if (source == HoleCarryForwardPuttingSource.Approach && carriedPuttingDistanceFromApproachMeters is not null)
        {
            carriedPuttingDistanceFromApproachMeters = null;
            changed = true;
        }

        if (source == HoleCarryForwardPuttingSource.AroundGreen && carriedPuttingDistanceFromAroundGreenMeters is not null)
        {
            carriedPuttingDistanceFromAroundGreenMeters = null;
            changed = true;
        }

        if (changed)
        {
            NotifyPuttingDistanceCarryForwardChanged();
        }
    }

    private void ClearCarriedPuttingDistanceIfManuallyChanged()
    {
        if (HoleCarryForwardService.ShouldClearCarriedDistance(
            ParsePuttingDistance(DistanceText),
            carriedPuttingDistanceFromApproachMeters))
        {
            carriedPuttingDistanceFromApproachMeters = null;
        }
    }

    private void ClearCarriedAroundGreenPuttingDistanceIfManuallyChanged()
    {
        if (HoleCarryForwardService.ShouldClearCarriedDistance(
            ParsePuttingDistance(DistanceText),
            carriedPuttingDistanceFromAroundGreenMeters))
        {
            carriedPuttingDistanceFromAroundGreenMeters = null;
        }
    }

    private void ClearCarriedAroundGreenDistanceIfManuallyChanged()
    {
        if (HoleCarryForwardService.ShouldClearCarriedDistance(
            ParseDistance(AroundGreenStartDistanceText),
            carriedAroundGreenStartDistanceFromApproachMeters))
        {
            carriedAroundGreenStartDistanceFromApproachMeters = null;
        }
    }

    private void ClearCarriedAroundGreenLieIfManuallyChanged()
    {
        if (HoleCarryForwardService.ShouldClearCarriedLie(
            AroundGreenStartLieText,
            carriedAroundGreenStartLieFromApproach))
        {
            carriedAroundGreenStartLieFromApproach = null;
        }
    }

    private static string FormatAroundGreenStartLie(ShotLie lie) => ShotLieLabels.FormatAroundGreenStart(lie);

    private static ShotLie ParseLie(string text) => ShotLieLabels.Parse(text);

    private static string FormatLie(ShotLie lie) => ShotLieLabels.Format(lie);

    private double GetApproachFinishStep()
    {
        return ParseLie(ApproachEndLieText) == ShotLie.Green
            ? GreenFinishDistanceStepMeters
            : ApproachDistanceStepMeters;
    }

    private static int GetApproachFinishDistanceDecimals() => 1;

    private double GetAroundGreenFinishStep()
    {
        return ParseLie(AroundGreenEndLieText) == ShotLie.Green
            ? GreenFinishDistanceStepMeters
            : AroundGreenDistanceStepMeters;
    }

    private static double ToMeters(double distance, DistanceUnit unit) => DistanceConversions.ToMeters(distance, unit);

    private static double YardsToMeters(double distanceYards) => DistanceConversions.YardsToMeters(distanceYards);

    private PuttingDistanceUnitPreference PuttingDistanceUnit => distanceUnitSettings.PuttingDistanceUnit;

    private double ParsePuttingDistance(string text) =>
        UiFormat.FromPreferredPuttingDistance(ParseDistance(text), PuttingDistanceUnit);

    private double ParseFinishDistance(string text, ShotLie lie) => lie == ShotLie.Green
        ? ParsePuttingDistance(text)
        : ParseDistance(text);

    private string FormatPuttingInputDistance(double distanceMeters, int decimals) =>
        DistanceInputParser.FormatSlider(UiFormat.ToPreferredPuttingDistance(distanceMeters, PuttingDistanceUnit), decimals);

    private string FormatFinishInputDistance(double distanceMeters, ShotLie lie, int decimals) => lie == ShotLie.Green
        ? FormatPuttingInputDistance(distanceMeters, decimals)
        : FormatSliderDistance(distanceMeters, decimals);

    private static double ParseDistance(string text) => DistanceInputParser.ParseOrZero(text);

    private static string FormatStoredDistance(double value, int decimals) => DistanceInputParser.FormatStored(value, decimals);

    private static string FormatSliderDistance(double value, int decimals) => DistanceInputParser.FormatSlider(value, decimals);
}
