namespace GolfSG.Services;

// A foreground measurement only; no health history or background tracking.
public sealed class PuttStepCounter : IDisposable
{
    public event Action<int>? StepsChanged;
#if ANDROID
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
    private int steps;
    private bool disposed;

    public async Task StartAsync()
    {
#if ANDROID
        manager = (Android.Hardware.SensorManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.SensorService);
        var sensor = manager?.GetDefaultSensor(Android.Hardware.SensorType.StepDetector);
        if (sensor is null) throw new InvalidOperationException("Telefonen understøtter ikke skridtmåling.");
        if (OperatingSystem.IsAndroidVersionAtLeast(29) &&
            await Permissions.RequestAsync<MotionPermission>() != PermissionStatus.Granted)
            throw new InvalidOperationException("Tillad fysisk aktivitet for at måle med skridt.");
        if (disposed) return;
        listener = new StepListener(() => StepsChanged?.Invoke(++steps));
        // Deliver reports immediately rather than permitting hardware batching.
        if (!manager!.RegisterListener(listener, sensor, Android.Hardware.SensorDelay.Normal, 0))
            throw new InvalidOperationException("Skridtmåleren kunne ikke startes.");
#elif IOS
        if (!CoreMotion.CMPedometer.IsStepCountingAvailable)
            throw new InvalidOperationException("Telefonen understøtter ikke skridtmåling.");
        if (disposed) return;
        pedometer = new CoreMotion.CMPedometer();
        pedometer.StartPedometerUpdates(Foundation.NSDate.Now, (data, error) =>
        {
            if (disposed) return;
            if (error is not null) StepsChanged?.Invoke(-1);
            else if (data is not null) StepsChanged?.Invoke(data.NumberOfSteps.Int32Value);
        });
        await Task.CompletedTask;
#else
        await Task.CompletedTask;
        throw new InvalidOperationException("Skridtmåling er kun tilgængelig på understøttede telefoner.");
#endif
    }

    public void Dispose()
    {
        disposed = true;
#if ANDROID
        if (listener is not null) { manager?.UnregisterListener(listener); listener.Dispose(); listener = null; }
#elif IOS
        pedometer?.StopPedometerUpdates();
        pedometer?.Dispose();
        pedometer = null;
#endif
    }
}
