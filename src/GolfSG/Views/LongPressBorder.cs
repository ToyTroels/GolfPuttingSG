namespace GolfSG.Views;

public sealed partial class LongPressBorder : Border
{
    private DateTime lastLongPress = DateTime.MinValue;
    private bool isPressed;

    public event EventHandler? LongPressed;
    public event EventHandler<bool>? PressedChanged;

    protected override void OnHandlerChanged()
    {
        DisconnectLongPress();
        base.OnHandlerChanged();
        ConnectLongPress();
    }

    private void SendLongPressed()
    {
        var now = DateTime.UtcNow;
        if (now - lastLongPress < TimeSpan.FromMilliseconds(800))
        {
            return;
        }

        lastLongPress = now;
        LongPressed?.Invoke(this, EventArgs.Empty);
    }

    private void SetPressed(bool value)
    {
        if (isPressed == value)
        {
            return;
        }

        isPressed = value;
        PressedChanged?.Invoke(this, value);
    }
}

#if ANDROID
public sealed partial class LongPressBorder
{
    private readonly List<Android.Views.View> longPressViews = [];

    private void ConnectLongPress()
    {
        if (Handler?.PlatformView is Android.Views.View view)
        {
            view.Post(() => AttachLongPress(view));
        }
    }

    private void DisconnectLongPress()
    {
        foreach (var view in longPressViews)
        {
            view.LongClick -= OnPlatformLongClick;
            view.Touch -= OnPlatformTouch;
        }

        longPressViews.Clear();
        SetPressed(false);
    }

    private void AttachLongPress(Android.Views.View view)
    {
        if (longPressViews.Contains(view))
        {
            return;
        }

        view.Clickable = true;
        view.LongClickable = true;
        view.LongClick += OnPlatformLongClick;
        view.Touch += OnPlatformTouch;
        longPressViews.Add(view);

        if (view is Android.Views.ViewGroup group)
        {
            for (var index = 0; index < group.ChildCount; index++)
            {
                var child = group.GetChildAt(index);
                if (child is not null)
                {
                    AttachLongPress(child);
                }
            }
        }
    }

    private void OnPlatformTouch(object? sender, Android.Views.View.TouchEventArgs e)
    {
        if (e.Event?.Action is Android.Views.MotionEventActions.Down)
        {
            SetPressed(true);
        }
        else if (e.Event?.Action is Android.Views.MotionEventActions.Up or
                 Android.Views.MotionEventActions.Cancel)
        {
            SetPressed(false);
        }

        e.Handled = false;
    }

    private void OnPlatformLongClick(object? sender, Android.Views.View.LongClickEventArgs e)
    {
        e.Handled = true;
        SetPressed(false);
        SendLongPressed();
    }
}
#elif IOS || MACCATALYST
public sealed partial class LongPressBorder
{
    private readonly List<(UIKit.UIView View, UIKit.UILongPressGestureRecognizer PressRecognizer, UIKit.UILongPressGestureRecognizer LongPressRecognizer)> recognizers = [];

    private void ConnectLongPress()
    {
        if (Handler?.PlatformView is UIKit.UIView view)
        {
            AttachLongPress(view);
        }
    }

    private void DisconnectLongPress()
    {
        foreach (var (view, pressRecognizer, longPressRecognizer) in recognizers)
        {
            view.RemoveGestureRecognizer(pressRecognizer);
            view.RemoveGestureRecognizer(longPressRecognizer);
            pressRecognizer.Dispose();
            longPressRecognizer.Dispose();
        }

        recognizers.Clear();
        SetPressed(false);
    }

    private void AttachLongPress(UIKit.UIView view)
    {
        if (recognizers.Any(item => item.View == view))
        {
            return;
        }

        view.UserInteractionEnabled = true;
        var pressRecognizer = new UIKit.UILongPressGestureRecognizer(OnPlatformPress)
        {
            CancelsTouchesInView = false,
            MinimumPressDuration = 0.01
        };
        var longPressRecognizer = new UIKit.UILongPressGestureRecognizer(OnPlatformLongPress)
        {
            CancelsTouchesInView = false
        };

        view.AddGestureRecognizer(pressRecognizer);
        view.AddGestureRecognizer(longPressRecognizer);
        recognizers.Add((view, pressRecognizer, longPressRecognizer));

        foreach (var child in view.Subviews)
        {
            AttachLongPress(child);
        }
    }

    private void OnPlatformPress(UIKit.UILongPressGestureRecognizer recognizer)
    {
        if (recognizer.State == UIKit.UIGestureRecognizerState.Began)
        {
            SetPressed(true);
        }
        else if (recognizer.State is UIKit.UIGestureRecognizerState.Ended or
                 UIKit.UIGestureRecognizerState.Cancelled or
                 UIKit.UIGestureRecognizerState.Failed)
        {
            SetPressed(false);
        }
    }

    private void OnPlatformLongPress(UIKit.UILongPressGestureRecognizer recognizer)
    {
        if (recognizer.State == UIKit.UIGestureRecognizerState.Began)
        {
            SetPressed(false);
            SendLongPressed();
        }
    }
}
#elif WINDOWS
public sealed partial class LongPressBorder
{
    private readonly List<Microsoft.UI.Xaml.FrameworkElement> longPressElements = [];

    private void ConnectLongPress()
    {
        if (Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement element)
        {
            AttachLongPress(element);
        }
    }

    private void DisconnectLongPress()
    {
        foreach (var element in longPressElements)
        {
            element.Holding -= OnPlatformHolding;
            element.PointerPressed -= OnPlatformPointerPressed;
            element.PointerReleased -= OnPlatformPointerReleased;
            element.PointerCanceled -= OnPlatformPointerReleased;
            element.PointerExited -= OnPlatformPointerReleased;
        }

        longPressElements.Clear();
        SetPressed(false);
    }

    private void AttachLongPress(Microsoft.UI.Xaml.DependencyObject element)
    {
        if (element is Microsoft.UI.Xaml.FrameworkElement frameworkElement &&
            !longPressElements.Contains(frameworkElement))
        {
            frameworkElement.Holding += OnPlatformHolding;
            frameworkElement.PointerPressed += OnPlatformPointerPressed;
            frameworkElement.PointerReleased += OnPlatformPointerReleased;
            frameworkElement.PointerCanceled += OnPlatformPointerReleased;
            frameworkElement.PointerExited += OnPlatformPointerReleased;
            longPressElements.Add(frameworkElement);
        }

        var childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(element);
        for (var index = 0; index < childCount; index++)
        {
            AttachLongPress(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(element, index));
        }
    }

    private void OnPlatformPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        SetPressed(true);
    }

    private void OnPlatformPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        SetPressed(false);
    }

    private void OnPlatformHolding(object sender, Microsoft.UI.Xaml.Input.HoldingRoutedEventArgs e)
    {
        if (e.HoldingState == Microsoft.UI.Input.HoldingState.Started)
        {
            e.Handled = true;
            SetPressed(false);
            SendLongPressed();
        }
    }
}
#else
public sealed partial class LongPressBorder
{
    private void ConnectLongPress()
    {
    }

    private void DisconnectLongPress()
    {
        SetPressed(false);
    }
}
#endif
