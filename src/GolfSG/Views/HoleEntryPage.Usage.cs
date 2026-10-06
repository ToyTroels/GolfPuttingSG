using GolfSG.Application.Services;
using Microsoft.Maui;

namespace GolfSG.Views;

public sealed partial class HoleEntryPage
{
    private void AttachUsageCounters(IVisualTreeElement element)
    {
        if (element is Button button)
        {
            button.Pressed += (_, _) =>
            {
                var text = button.Text ?? "";
                var method = text.StartsWith('+') || text.StartsWith('−') || text is "+" or "-"
                    ? "stepper" : text.EndsWith(" m") || text.EndsWith(" ft") ? "distance_preset" : "button";
                UsageDiagnostics.Action(viewModel, useGuidedInput, method);
            };
        }
        if (element is Entry entry)
        {
            entry.Focused += (_, _) => UsageDiagnostics.Action(viewModel, useGuidedInput, "inline_distance_edit");
        }
        if (element is View view)
        {
            foreach (var tap in view.GestureRecognizers.OfType<TapGestureRecognizer>())
            {
                tap.Tapped += (_, _) => UsageDiagnostics.Action(viewModel, useGuidedInput, "distance_popup");
            }
        }
        foreach (var child in element.GetVisualChildren()) AttachUsageCounters(child);
    }
}
