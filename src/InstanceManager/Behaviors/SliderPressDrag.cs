using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace InstanceManager.Behaviors;

public static class SliderPressDrag
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(SliderPressDrag),
            new PropertyMetadata(false, OnEnabledChanged));

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider)
            return;

        if ((bool)e.NewValue)
        {
            slider.PreviewMouseLeftButtonDown += OnDown;
            slider.PreviewMouseMove += OnMove;
            slider.PreviewMouseLeftButtonUp += OnUp;
        }
        else
        {
            slider.PreviewMouseLeftButtonDown -= OnDown;
            slider.PreviewMouseMove -= OnMove;
            slider.PreviewMouseLeftButtonUp -= OnUp;
        }
    }

    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        var slider = (Slider)sender;
        if (slider.Template?.FindName("PART_Track", slider) is not Track track)
            return;

        slider.Focus();
        MoveTo(slider, track, e.GetPosition(track));
        slider.CaptureMouse();
        e.Handled = true;
    }

    private static void OnMove(object sender, MouseEventArgs e)
    {
        var slider = (Slider)sender;
        if (!slider.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (slider.Template?.FindName("PART_Track", slider) is Track track)
            MoveTo(slider, track, e.GetPosition(track));
    }

    private static void OnUp(object sender, MouseButtonEventArgs e)
    {
        var slider = (Slider)sender;
        if (!slider.IsMouseCaptured)
            return;
        slider.ReleaseMouseCapture();
        e.Handled = true;
    }

    private static void MoveTo(Slider slider, Track track, Point point)
    {
        double thumb = track.Thumb?.ActualWidth ?? 0;
        double value = ValueAt(point.X, track.ActualWidth, thumb, slider.Minimum, slider.Maximum);
        slider.SetCurrentValue(RangeBase.ValueProperty, Snap(slider, value));
    }

    internal static double ValueAt(double x, double trackWidth, double thumbWidth, double min, double max)
    {
        double travel = trackWidth - thumbWidth;
        if (travel <= 0)
            return min;
        double fraction = Math.Clamp((x - thumbWidth / 2) / travel, 0, 1);
        return min + fraction * (max - min);
    }

    internal static double Snap(Slider slider, double value)
    {
        value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
        {
            double steps = Math.Round((value - slider.Minimum) / slider.TickFrequency);
            value = Math.Min(slider.Maximum, slider.Minimum + steps * slider.TickFrequency);
        }
        return value;
    }
}
