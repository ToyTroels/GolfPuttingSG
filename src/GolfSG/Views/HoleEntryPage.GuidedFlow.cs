using GolfSG.Application.ViewModels;

namespace GolfSG.Views;

public sealed partial class HoleEntryPage
{
    private async Task<bool> PauseBeforeAutomaticPaneChangeAsync()
    {
        CancelGuidedAutoAdvance();
        guidedAutoAdvanceCts = new CancellationTokenSource();
        var token = guidedAutoAdvanceCts.Token;
        var hole = viewModel;
        try
        {
            await Task.Delay(350, token);
            return !token.IsCancellationRequested && ReferenceEquals(hole, viewModel);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task AdvanceAfterAroundGreenDistanceAsync()
    {
        if (!viewModel.CanAdvanceAroundGreenStep || !viewModel.IsPuttingInputVisible ||
            viewModel.CanAddAnotherAroundGreenShot) return;
        if (!await PauseBeforeAutomaticPaneChangeAsync()) return;
        if (!viewModel.CanAdvanceAroundGreenStep || !viewModel.IsPuttingInputVisible ||
            viewModel.CanAddAnotherAroundGreenShot) return;
        if (useGuidedInput)
        {
            if (activeStep != HoleEntryStep.AroundGreen) return;
            activeStep = HoleEntryStep.Putting;
            UpdateGuidedStepVisibility();
            await ScrollInputToTopAsync(animated: true);
        }
        else if (openNextInputPaneAsync is not null)
        {
            await openNextInputPaneAsync(HoleEntryStep.AroundGreen);
        }
    }

    private async Task AdvanceAfterApproachDistanceAsync()
    {
        if (!viewModel.CanAdvanceApproachStep) return;
        if (!await PauseBeforeAutomaticPaneChangeAsync() || !viewModel.CanAdvanceApproachStep) return;
        if (useGuidedInput)
        {
            var steps = GetVisibleSteps();
            if (activeStep == HoleEntryStep.Approach && steps.Count > 1)
            {
                activeStep = steps[1];
                UpdateGuidedStepVisibility();
                await ScrollInputToTopAsync(animated: true);
            }
        }
        else if (openNextInputPaneAsync is not null)
        {
            await openNextInputPaneAsync(HoleEntryStep.Approach);
        }
    }

    private async Task AdvanceGuidedPuttingAfterQuickActionAsync()
    {
        if (!await PauseBeforeAutomaticPaneChangeAsync()) return;
        if (activeStep == HoleEntryStep.Putting &&
            viewModel.CanAdvancePuttingStep &&
            !suppressGuidedAutoAdvanceAfterBack)
        {
            await GoForwardInGuidedFlowAsync();
        }
    }

    private void UpdateGuidedStepVisibility()
    {
        if (!useGuidedInput)
        {
            return;
        }

        var visibleSteps = GetVisibleSteps();
        if (visibleSteps.Count == 0)
        {
            SetSectionVisibility(false, false, false);
            if (guidedStepLabel is not null)
            {
                guidedStepLabel.Text = string.Empty;
            }

            if (guidedBackButton is not null)
            {
                guidedBackButton.Text = "Til oversigt";
            }

            if (guidedNextButton is not null)
            {
                guidedNextButton.Text = GetNextHole() is null ? "Til oversigt" : "Næste hul";
            }

            return;
        }

        if (!visibleSteps.Contains(activeStep))
        {
            activeStep = visibleSteps[0];
        }

        var index = visibleSteps.IndexOf(activeStep);
        SetSectionVisibility(
            activeStep == HoleEntryStep.Approach && viewModel.IsApproachInputVisible,
            activeStep == HoleEntryStep.AroundGreen && viewModel.IsAroundGreenInputVisible,
            activeStep == HoleEntryStep.Putting && viewModel.IsPuttingInputVisible);

        if (guidedStepLabel is not null)
        {
            guidedStepLabel.Text = $"{index + 1} / {visibleSteps.Count} - {GetStepLabel(activeStep)}";
            if (GolfSG.Application.Services.FeatureSettings.EnableBetaFeatures && activeStep != HoleEntryStep.Putting)
            {
                guidedStepLabel.Text += " · Beta";
            }
        }

        if (guidedBackButton is not null)
        {
            guidedBackButton.Text = index > 0 ? "Forrige" : "Til oversigt";
        }

        if (guidedNextButton is not null)
        {
            guidedNextButton.Text = index < visibleSteps.Count - 1
                ? $"Næste: {GetStepLabel(visibleSteps[index + 1])}"
                : GetNextHole() is null ? "Til oversigt" : "Næste hul";
        }
    }

    private void SetSectionVisibility(bool showApproach, bool showAroundGreen, bool showPutting)
    {
        if (approachSectionView is not null)
        {
            approachSectionView.IsVisible = showApproach;
        }

        if (aroundGreenSectionView is not null)
        {
            aroundGreenSectionView.IsVisible = showAroundGreen;
        }

        if (puttingSectionView is not null)
        {
            puttingSectionView.IsVisible = showPutting;
        }
    }

    private bool CanAdvanceCurrentGuidedStep()
    {
        return activeStep switch
        {
            HoleEntryStep.Approach => viewModel.CanAdvanceApproachStep,
            HoleEntryStep.AroundGreen => viewModel.CanAdvanceAroundGreenStep,
            HoleEntryStep.Putting => viewModel.CanAdvancePuttingStep,
            _ => false
        };
    }

    private bool HasPreviousGuidedStep()
    {
        var visibleSteps = GetVisibleSteps();
        return visibleSteps.IndexOf(activeStep) > 0;
    }

    private void ScheduleGuidedAutoAdvance(HoleEntryStep step)
    {
        CancelGuidedAutoAdvance();
        guidedAutoAdvanceCts = new CancellationTokenSource();
        var token = guidedAutoAdvanceCts.Token;
        _ = AutoAdvanceGuidedStepAsync(step, token);
    }

    private async Task AutoAdvanceGuidedStepAsync(HoleEntryStep step, CancellationToken token)
    {
        try
        {
            await Task.Delay(650, token);
            if (token.IsCancellationRequested ||
                isGuidedAutoAdvancing ||
                activeStep != step ||
                !CanAdvanceCurrentGuidedStep())
            {
                return;
            }

            isGuidedAutoAdvancing = true;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (activeStep == step && CanAdvanceCurrentGuidedStep())
                {
                    await GoForwardInGuidedFlowAsync();
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isGuidedAutoAdvancing = false;
        }
    }

    private void CancelGuidedAutoAdvance()
    {
        guidedAutoAdvanceCts?.Cancel();
        guidedAutoAdvanceCts?.Dispose();
        guidedAutoAdvanceCts = null;
    }

    private async Task GoBackInGuidedFlowAsync()
    {
        CancelGuidedAutoAdvance();
        var visibleSteps = GetVisibleSteps();
        var index = visibleSteps.IndexOf(activeStep);
        if (index > 0)
        {
            activeStep = visibleSteps[index - 1];
            suppressGuidedAutoAdvanceAfterBack = true;
            UpdateGuidedStepVisibility();
            await ScrollInputToTopAsync();
            return;
        }

        await GoToOverviewAsync();
    }

    private async Task GoForwardInGuidedFlowAsync()
    {
        CancelGuidedAutoAdvance();
        var visibleSteps = GetVisibleSteps();
        var index = visibleSteps.IndexOf(activeStep);
        if (index >= 0 && index < visibleSteps.Count - 1)
        {
            activeStep = visibleSteps[index + 1];
            UpdateGuidedStepVisibility();
            await ScrollInputToTopAsync();
            return;
        }

        await GoToNextHoleOrOverviewAsync();
    }

    private List<HoleEntryStep> GetVisibleSteps()
    {
        var steps = new List<HoleEntryStep>();
        if (viewModel.IsApproachInputVisible)
        {
            steps.Add(HoleEntryStep.Approach);
        }

        if (viewModel.IsAroundGreenInputVisible)
        {
            steps.Add(HoleEntryStep.AroundGreen);
        }

        if (viewModel.IsPuttingInputVisible)
        {
            steps.Add(HoleEntryStep.Putting);
        }

        return steps;
    }

    private static string GetStepLabel(HoleEntryStep step)
    {
        return step switch
        {
            HoleEntryStep.Approach => "Approach",
            HoleEntryStep.AroundGreen => "Omkring green",
            HoleEntryStep.Putting => "Putting",
            _ => string.Empty
        };
    }

    private async Task ScrollInputToTopAsync(bool animated = false)
    {
        // Let the newly opened section update its layout before resetting the offset.
        await Task.Yield();
        if (holeScrollView?.Handler is not null)
        {
            await holeScrollView.ScrollToAsync(0, 0, animated);
        }
    }

    private async Task GoToNextHoleOrOverviewAsync()
    {
        var nextHole = GetNextHole();
        if (nextHole is null)
        {
            await GoToOverviewAsync();
            return;
        }

        await GoToHoleAsync(nextHole);
    }

    private async Task ConfirmCloseRoundAsync()
    {
        var closeRound = await DisplayAlertAsync(
            "Luk runde?",
            "Runden er gemt automatisk. Du kan forts\u00e6tte den fra forsiden.",
            "Luk runde",
            "Bliv her");

        if (closeRound)
        {
            await roundViewModel.FlushAutosaveAsync();
            await this.RunNavigationOnceAsync(() => Navigation.PopToRootAsync());
        }
    }

    private async Task GoToHoleAsync(HoleInputViewModel? hole)
    {
        if (hole is null)
        {
            return;
        }

        await this.RunNavigationOnceAsync(async () =>
        {
            CancelGuidedAutoAdvance();
            GolfSG.Application.Services.UsageDiagnostics.FinishVisit(viewModel);
            suppressGuidedAutoAdvanceAfterBack = useGuidedInput && IsPreviousHole(hole);
            if (isViewModelSubscribed)
            {
                viewModel.PropertyChanged -= OnHoleInputPropertyChanged;
            }

            // Rebind the existing controls rather than constructing and navigating to
            // another large native control tree for every hole.
            viewModel = hole;
            GolfSG.Application.Services.UsageDiagnostics.BeginVisit(hole);
            BindingContext = hole;
            Title = hole.Title;
            SemanticProperties.SetDescription(this, $"Input for hul {hole.HoleNumber}");
            if (nextHoleButton is not null)
            {
                var isLastHole = GetNextHole() is null;
                nextHoleButton.Text = isLastHole ? "Til oversigt" : "Næste hul";
                Grid.SetColumn(nextHoleButton, isLastHole ? 0 : 1);
                Grid.SetColumnSpan(nextHoleButton, isLastHole ? 2 : 1);
                if (overviewButton is not null)
                {
                    overviewButton.IsVisible = !isLastHole;
                }
            }
            var visibleSteps = GetVisibleSteps();
            activeStep = visibleSteps.Count > 0 ? visibleSteps[0] : HoleEntryStep.Putting;
            if (isViewModelSubscribed)
            {
                viewModel.PropertyChanged += OnHoleInputPropertyChanged;
            }
            UpdateGuidedStepVisibility();
            if (!useGuidedInput)
            {
                openFirstVisibleInputSection?.Invoke();
            }
            roundViewModel.SetCurrentHole(hole.HoleNumber);
            await ScrollInputToTopAsync();
        });
    }

    private async Task GoToOverviewAsync()
    {
        CancelGuidedAutoAdvance();
        await this.RunNavigationOnceAsync(async () =>
        {
            await roundViewModel.FlushAutosaveAsync();

            GolfSG.Application.Services.UsageDiagnostics.FinishVisit(viewModel);

            // Remove any older hole pages so a single pop always reveals the overview.
            foreach (var page in Navigation.NavigationStack.TakeWhile(page => page != this)
                         .OfType<HoleEntryPage>().ToArray())
            {
                Navigation.RemovePage(page);
            }

            await Navigation.PopAsync();
        });
    }

    private Task NavigateBackFromHeaderAsync()
    {
        var previousHole = GetPreviousHole();
        return previousHole is null
            ? GoToOverviewAsync()
            : GoToHoleAsync(previousHole);
    }

    private bool IsPreviousHole(HoleInputViewModel hole)
    {
        var currentIndex = roundViewModel.Holes.IndexOf(viewModel);
        var targetIndex = roundViewModel.Holes.IndexOf(hole);
        return currentIndex >= 0 && targetIndex >= 0 && targetIndex < currentIndex;
    }

    private HoleInputViewModel? GetPreviousHole()
    {
        var currentIndex = roundViewModel.Holes.IndexOf(viewModel);
        var previousIndex = currentIndex - 1;
        return previousIndex >= 0 ? roundViewModel.Holes[previousIndex] : null;
    }

    private HoleInputViewModel? GetNextHole()
    {
        var currentIndex = roundViewModel.Holes.IndexOf(viewModel);
        var nextIndex = currentIndex + 1;
        return currentIndex >= 0 && nextIndex < roundViewModel.Holes.Count
            ? roundViewModel.Holes[nextIndex]
            : null;
    }

}
