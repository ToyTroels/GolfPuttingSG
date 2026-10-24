namespace GolfSG.Services;

// A foreground measurement only; no health history or background tracking.
public sealed class PuttStepCounter : IPuttStepCounter
{
    public event Action<int>? StepsChanged;
    public event Action<string>? Failed;
#if ANDROID
    private int steps;
    private Android.Hardware.SensorManager? manager;
    private StepListener? listener;
    private sealed class MotionPermission : Permissions.BasePlatformPermission
    {
        public MotionPermission() { }
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [("android.permission.ACTIVITY_RECOGNITION", true)];
    }
    private sealed class StepListener(Action onStep) : Java.Lang.Object, Android.Hardware.ISensorEventListener
    {
        public void OnAccuracyChanged(Android.Hardware.Sensor? sensor, Android.Hardware.SensorStatus accuracy) { }
        public void OnSensorChanged(Android.Hardware.SensorEvent? e) { if (e is not null) onStep(); }
    }
#elif IOS
    private CoreMotion.CMPedometer? pedometer;
#endif
    private volatile bool disposed;
    private bool started;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) throw new InvalidOperationException("Skridtmåleren er allerede startet.");
        started = true;
        cancellationToken.ThrowIfCancellationRequested();
#if ANDROID
        manager = (Android.Hardware.SensorManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.SensorService);
        var sensor = manager?.GetDefaultSensor(Android.Hardware.SensorType.StepDetector);
        if (sensor is null) throw new InvalidOperationException("Telefonen understøtter ikke skridtmåling.");
        if (OperatingSystem.IsAndroidVersionAtLeast(29) &&
            await Permissions.RequestAsync<MotionPermission>() != PermissionStatus.Granted)
            throw new InvalidOperationException("Tillad fysisk aktivitet for at måle med skridt.");
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
        listener = new StepListener(() =>
        {
            if (!disposed) ReportSteps(Interlocked.Increment(ref steps));
        });
        // Deliver reports immediately rather than permitting hardware batching.
        if (!manager!.RegisterListener(listener, sensor, Android.Hardware.SensorDelay.Normal, 0))
            throw new InvalidOperationException("Skridtmåleren kunne ikke startes.");
#elif IOS
        if (!CoreMotion.CMPedometer.IsStepCountingAvailable)
            throw new InvalidOperationException("Telefonen understøtter ikke skridtmåling.");
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
        pedometer = new CoreMotion.CMPedometer();
        pedometer.StartPedometerUpdates(Foundation.NSDate.Now, (data, error) =>
        {
            if (disposed) return;
            if (error is not null) ReportFailure("Motionstilladelse mangler, eller skridtmålingen kunne ikke fortsætte.");
            else if (data is not null) ReportSteps(data.NumberOfSteps.Int32Value);
        });
        await Task.CompletedTask;
#else
        await Task.CompletedTask;
        throw new InvalidOperationException("Skridtmåling er kun tilgængelig på understøttede telefoner.");
#endif
    }

    private void ReportFailure(string message)
    {
        if (!disposed) Failed?.Invoke(message);
    }

    private void ReportSteps(int count)
    {
        if (!disposed) StepsChanged?.Invoke(count);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
#if ANDROID
        if (listener is not null)
        {
            manager?.UnregisterListener(listener);
            listener.Dispose();
            listener = null;
        }
#elif IOS
        pedometer?.StopPedometerUpdates();
        pedometer?.Dispose();
        pedometer = null;
#endif
        StepsChanged = null;
        Failed = null;
    }
}
