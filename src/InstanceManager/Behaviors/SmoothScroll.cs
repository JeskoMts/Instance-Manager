using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InstanceManager.Behaviors;

public static class SmoothScroll
{
    private const double StepMultiplier = 1.2;
    private const double SmoothingTau = 0.10;
    private const double SettleThreshold = 0.4;
    private const double MaxFrameSeconds = 0.05;

    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(SmoothScroll),
            new PropertyMetadata(false, OnEnabledChanged));

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State", typeof(ScrollState), typeof(SmoothScroll), new PropertyMetadata(null));

    private static readonly DependencyProperty ResolvedViewerProperty =
        DependencyProperty.RegisterAttached(
            "ResolvedViewer", typeof(ScrollViewer), typeof(SmoothScroll), new PropertyMetadata(null));

    private static readonly List<ScrollState> Active = new();
    private static bool _renderingHooked;
    private static TimeSpan _lastRenderTime;

    private sealed class ScrollState
    {
        public ScrollState(ScrollViewer viewer) => Viewer = viewer;
        public ScrollViewer Viewer { get; }
        public double Current { get; set; }
        public double Target { get; set; }
        public bool IsActive { get; set; }
    }

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        if ((bool)e.NewValue)
            element.PreviewMouseWheel += OnPreviewMouseWheel;
        else
            element.PreviewMouseWheel -= OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var host = (UIElement)sender;
        ScrollViewer? viewer = ResolveViewer(host);
        if (viewer == null || viewer.ScrollableHeight <= 0)
            return;

        if (IsOverWheelConsumingControl(e.OriginalSource as DependencyObject, host))
            return;

        e.Handled = true;

        ScrollState state = GetOrCreateState(viewer);

        if (!state.IsActive)
            state.Current = state.Target = viewer.VerticalOffset;

        state.Target = Math.Clamp(state.Target - e.Delta * StepMultiplier, 0, viewer.ScrollableHeight);

        if (!state.IsActive)
        {
            state.IsActive = true;
            Active.Add(state);
        }
        EnsureRendering();
    }

    private static ScrollState GetOrCreateState(ScrollViewer viewer)
    {
        if (viewer.GetValue(StateProperty) is ScrollState existing)
            return existing;

        var state = new ScrollState(viewer);
        viewer.SetValue(StateProperty, state);
        return state;
    }

    private static void EnsureRendering()
    {
        if (_renderingHooked)
            return;
        _lastRenderTime = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
        _renderingHooked = true;
    }

    private static void OnRendering(object? sender, EventArgs e)
    {
        TimeSpan now = e is RenderingEventArgs re ? re.RenderingTime : _lastRenderTime;
        double dt = _lastRenderTime == TimeSpan.Zero ? 1.0 / 60.0 : (now - _lastRenderTime).TotalSeconds;
        _lastRenderTime = now;
        if (dt <= 0)
            return;
        dt = Math.Min(dt, MaxFrameSeconds);

        double factor = 1 - Math.Exp(-dt / SmoothingTau);

        for (int i = Active.Count - 1; i >= 0; i--)
        {
            ScrollState state = Active[i];
            ScrollViewer viewer = state.Viewer;

            double max = viewer.ScrollableHeight;
            state.Target = Math.Clamp(state.Target, 0, max);
            state.Current = Math.Clamp(state.Current, 0, max);

            double diff = state.Target - state.Current;
            if (Math.Abs(diff) < SettleThreshold)
            {
                viewer.ScrollToVerticalOffset(state.Target);
                state.Current = state.Target;
                state.IsActive = false;
                Active.RemoveAt(i);
                continue;
            }

            state.Current += diff * factor;
            viewer.ScrollToVerticalOffset(state.Current);
        }

        if (Active.Count == 0)
        {
            CompositionTarget.Rendering -= OnRendering;
            _renderingHooked = false;
        }
    }

    private static ScrollViewer? ResolveViewer(UIElement host)
    {
        if (host is ScrollViewer self)
            return self;

        if (host.GetValue(ResolvedViewerProperty) is ScrollViewer cached)
            return cached;

        ScrollViewer? found = FindDescendantScrollViewer((DependencyObject)host);
        if (found != null)
            host.SetValue(ResolvedViewerProperty, found);
        return found;
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
                return viewer;
            ScrollViewer? nested = FindDescendantScrollViewer(child);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private static bool IsOverWheelConsumingControl(DependencyObject? source, DependencyObject root)
    {
        DependencyObject? node = source;
        while (node != null && node != root)
        {
            if (node is Slider)
                return true;
            if (node is ComboBox { IsDropDownOpen: true })
                return true;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return false;
    }
}
