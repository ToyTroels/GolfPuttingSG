using System.Reflection;
using GolfSG.Services;
using GolfSG.Views;
using Microsoft.Maui.Dispatching;

namespace GolfSG.Tests;

[TestClass]
public sealed class PuttDistancePageTests
{
    private IDispatcherProvider? previousDispatcher;

    [TestInitialize]
    public void Setup()
    {
        previousDispatcher = DispatcherProvider.Current;
        DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    }

    [TestCleanup]
    public void Cleanup() => DispatcherProvider.SetCurrent(previousDispatcher!);

    [TestMethod]
    public async Task BackgroundingDuringWarmupDisposesCounterAndDoesNotEnableDoneLater()
    {
        var counter = new FakeCounter();
        var warmup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = Create(counter, (_, token) => warmup.Task.WaitAsync(token));
        var start = InvokeAsync(page, "StartAsync");
        Invoke(page, "OnWindowDeactivated", null, EventArgs.Empty);
        warmup.TrySetResult();
        await start.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(counter.Disposed);
        Assert.IsFalse(Control<Button>(page, "putt-measure.done").IsEnabled);
        Assert.IsFalse(Control<Button>(page, "putt-measure.apply").IsEnabled);
    }

    [TestMethod]
    public async Task LeavingWhileWaitingForPermissionCannotRestartCounter()
    {
        var counter = new FakeCounter { Permission = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var page = Create(counter, (_, token) => Task.Delay(Timeout.Infinite, token));
        var start = InvokeAsync(page, "StartAsync");
        Invoke(page, "OnDisappearing");
        counter.Permission.SetResult();
        await start.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(counter.Disposed);
        Assert.IsTrue(Control<Button>(page, "putt-measure.start").IsEnabled);
        Assert.IsFalse(Control<Button>(page, "putt-measure.done").IsEnabled);
    }

    [TestMethod]
    public async Task CompletedMeasurementSurvivesLateCallbacksAndCanBeCorrected()
    {
        var counter = new FakeCounter();
        var page = Create(counter, (_, _) => Task.CompletedTask);
        await InvokeAsync(page, "StartAsync");
        counter.Emit(4);
        await InvokeAsync(page, "FinishAsync");
        Assert.IsTrue(counter.Disposed);
        Assert.IsTrue(Control<Button>(page, "putt-measure.apply").IsEnabled);
        counter.Emit(100);
        Assert.IsTrue(Control<Button>(page, "putt-measure.apply").IsEnabled,
            "Reports from a disposed sensor must not overwrite the accepted result.");
        ((IButtonController)Control<Button>(page, "putt-measure.remove-step")).SendClicked();
        Assert.IsTrue(Control<Button>(page, "putt-measure.apply").IsEnabled);
    }

    [TestMethod]
    public async Task SensorFailureInvalidatesResultAndReleasesControls()
    {
        var counter = new FakeCounter();
        var page = Create(counter, (_, _) => Task.CompletedTask);
        await InvokeAsync(page, "StartAsync");
        counter.Emit(4);
        counter.Fail();
        Assert.IsTrue(counter.Disposed);
        Assert.IsFalse(Control<Button>(page, "putt-measure.apply").IsEnabled);
        Assert.IsTrue(Control<Button>(page, "putt-measure.start").IsEnabled);
    }

    [TestMethod]
    public async Task InterruptedFinishingCannotPublishAPartialResult()
    {
        var counter = new FakeCounter();
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = Create(counter, (duration, token) => duration.TotalSeconds == 3 ? finish.Task.WaitAsync(token) : Task.CompletedTask);
        await InvokeAsync(page, "StartAsync");
        counter.Emit(4);
        var completing = InvokeAsync(page, "FinishAsync");
        Invoke(page, "OnDisappearing");
        finish.TrySetResult();
        await completing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(counter.Disposed);
        Assert.IsFalse(Control<Button>(page, "putt-measure.apply").IsEnabled);
    }

    [TestMethod]
    public void InvalidSavedStepLengthFallsBackToDefault()
    {
        var preferences = new MemoryPreferences();
        preferences.Set("putt-step-length-cm", double.NaN);
        var page = new PuttDistancePage(_ => { }, preferences: preferences);
        Assert.AreEqual("75", Control<Entry>(page, "putt-measure.step-length").Text);
    }

    private static PuttDistancePage Create(FakeCounter counter, Func<TimeSpan, CancellationToken, Task> delay)
    {
        var page = new PuttDistancePage(_ => { }, () => counter, new MemoryPreferences(), delay);
        Invoke(page, "OnAppearing");
        return page;
    }

    private static void Invoke(object page, string name, params object?[] args) =>
        page.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(page, args);

    private static Task InvokeAsync(object page, string name) =>
        (Task)page.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(page, null)!;

    private static T Control<T>(Element page, string id) where T : Element
    {
        if (page is T match && page.AutomationId == id) return match;
        foreach (var child in ((IVisualTreeElement)page).GetVisualChildren().OfType<Element>())
        {
            try { return Control<T>(child, id); }
            catch (KeyNotFoundException) { }
        }
        throw new KeyNotFoundException(id);
    }

    private sealed class FakeCounter : IPuttStepCounter
    {
        public event Action<int>? StepsChanged;
        public event Action<string>? Failed;
        public TaskCompletionSource? Permission { get; init; }
        public bool Disposed { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken = default) => Permission?.Task ?? Task.CompletedTask;
        // Deliberately allow late callbacks to exercise the page's session guards.
        public void Emit(int steps) => StepsChanged?.Invoke(steps);
        public void Fail() => Failed?.Invoke("Test sensor failure");
        public void Dispose() => Disposed = true;
    }

    private sealed class MemoryPreferences : IPreferences
    {
        private readonly Dictionary<string, object> values = new();
        public bool ContainsKey(string key, string? sharedName = null) => values.ContainsKey(key);
        public void Remove(string key, string? sharedName = null) => values.Remove(key);
        public void Clear(string? sharedName = null) => values.Clear();
        public void Set<T>(string key, T value, string? sharedName = null) => values[key] = value!;
        public T Get<T>(string key, T defaultValue, string? sharedName = null) => values.TryGetValue(key, out var value) ? (T)value : defaultValue;
    }

    private sealed class ImmediateDispatcherProvider : IDispatcherProvider
    {
        private readonly IDispatcher dispatcher = new ImmediateDispatcher();
        public IDispatcher GetForCurrentThread() => dispatcher;
    }
    private sealed class ImmediateDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;
        public bool Dispatch(Action action) { action(); return true; }
        public bool DispatchDelayed(TimeSpan delay, Action action) => throw new NotSupportedException();
        public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
    }
}
