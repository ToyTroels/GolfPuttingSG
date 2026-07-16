using System.Runtime.CompilerServices;
using GolfSG.Application.Common;

namespace GolfSG.Views;

internal static class PageActionGuardExtensions
{
    private static readonly ConditionalWeakTable<Page, PageActionGates> Gates = new();

    public static Task<bool> RunActionOnceAsync(this Page page, Func<Task> action) =>
        Gates.GetValue(page, static _ => new PageActionGates()).Action.RunAsync(action);

    public static Task<bool> RunNavigationOnceAsync(this Page page, Func<Task> navigation) =>
        Gates.GetValue(page, static _ => new PageActionGates()).Navigation.RunAsync(navigation);

    private sealed class PageActionGates
    {
        public AsyncActionGate Action { get; } = new();
        public AsyncActionGate Navigation { get; } = new();
    }
}
