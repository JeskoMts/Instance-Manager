using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using InstanceManager.Behaviors;
using InstanceManager.Models;
using InstanceManager.ViewModels;

namespace InstanceManager.Views.Settings;

public partial class AppearanceSettings : UserControl
{
    private Point _themeDragStart;
    private Point _themeGrabOffset;
    private ThemeDefinition? _dragTheme;
    private FrameworkElement? _themeDragSource;
    private DragAdorner? _themeDragAdorner;

    public AppearanceSettings() => InitializeComponent();

    private ShellViewModel? Vm => DataContext as ShellViewModel;

    private void ThemeCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThemeDefinition theme } fe)
        {
            _dragTheme = theme;
            _themeDragSource = fe;
            _themeDragStart = e.GetPosition(null);
            _themeGrabOffset = e.GetPosition(fe);
            e.Handled = true;
        }
    }

    private void ThemeCard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_themeDragSource is { DataContext: ThemeDefinition theme }
            && ReferenceEquals(_dragTheme, theme)
            && Vm is { } vm)
        {
            vm.Theme.Activate(theme);
        }
        _dragTheme = null;
        _themeDragSource = null;
    }

    private void ThemeCard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTheme is not { } theme || _themeDragSource is not { } source || e.LeftButton != MouseButtonState.Pressed)
            return;

        Point pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _themeDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _themeDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragTheme = null;
        _themeDragSource = null;

        AdornerLayer? layer = AdornerLayer.GetAdornerLayer(ThemesList);
        if (layer != null)
        {
            _themeDragAdorner = new DragAdorner(ThemesList, DragAdorner.CreateGhost(source));
            _themeDragAdorner.SetPosition(SubtractThemeGrab(e.GetPosition(ThemesList)));
            layer.Add(_themeDragAdorner);
        }

        source.Opacity = 0.4;
        try
        {
            DragDrop.DoDragDrop(source, new DataObject(typeof(ThemeDefinition), theme), DragDropEffects.Move);
        }
        finally
        {
            source.Opacity = 1.0;
            if (_themeDragAdorner != null)
            {
                layer?.Remove(_themeDragAdorner);
                _themeDragAdorner = null;
            }
        }
    }

    private Point SubtractThemeGrab(Point cursor) => new(cursor.X - _themeGrabOffset.X, cursor.Y - _themeGrabOffset.Y);

    private void ThemesList_DragOver(object sender, DragEventArgs e)
    {
        _themeDragAdorner?.SetPosition(SubtractThemeGrab(e.GetPosition(ThemesList)));
        TryGetDraggedTheme(e.Data, out ThemeDefinition? draggedTheme);
        e.Effects = ThemeDragDecision.DropEffectFor(draggedTheme);
        e.Handled = true;
    }

    private void ThemesList_Drop(object sender, DragEventArgs e) => e.Handled = true;

    private void ThemeCard_DragOver(object sender, DragEventArgs e)
    {
        _themeDragAdorner?.SetPosition(SubtractThemeGrab(e.GetPosition(ThemesList)));
        TryGetDraggedTheme(e.Data, out ThemeDefinition? draggedTheme);
        bool valid = TryGetThemeReorderTarget(sender, e, out _);
        e.Effects = ThemeDragDecision.DropEffectFor(draggedTheme);
        SetThemeDropIndicator(sender as DependencyObject, valid);
        e.Handled = true;
    }

    private void ThemeCard_DragLeave(object sender, DragEventArgs e)
    {
        SetThemeDropIndicator(sender as DependencyObject, false);
        e.Handled = true;
    }

    private void ThemeCard_Drop(object sender, DragEventArgs e)
    {
        SetThemeDropIndicator(sender as DependencyObject, false);
        if (TryGetThemeReorderTarget(sender, e, out (ThemeDefinition Dragged, ThemeDefinition Target) t) && Vm is { } vm)
        {
            IReadOnlyList<ThemeDefinition> desired = ThemeSwap.SwapPair(vm.Theme.Themes, t.Dragged, t.Target);
            Dictionary<ThemeDefinition, Point> positionsBeforeSwap = CaptureThemePositions(includeRenderTransform: true);
            vm.Theme.ApplyOrder(desired);
            AnimateThemeReorder(positionsBeforeSwap);
        }
        e.Handled = true;
    }

    private static void SetThemeDropIndicator(DependencyObject? item, bool on)
    {
        if (item != null)
            ThemeDrag.SetIsDropTarget(item, on);
    }

    private static bool TryGetThemeReorderTarget(object sender, DragEventArgs e, out (ThemeDefinition Dragged, ThemeDefinition Target) target)
    {
        target = default;
        if (sender is not FrameworkElement { DataContext: ThemeDefinition targetTheme })
            return false;
        if (!TryGetDraggedTheme(e.Data, out ThemeDefinition? dragged))
            return false;
        if (!ThemeDragDecision.CanReorder(dragged, targetTheme))
            return false;
        target = (dragged!, targetTheme);
        return true;
    }

    private static bool TryGetDraggedTheme(IDataObject data, out ThemeDefinition? draggedTheme)
    {
        draggedTheme = data.GetData(typeof(ThemeDefinition)) as ThemeDefinition;
        return draggedTheme is not null;
    }

    private Dictionary<ThemeDefinition, Point> CaptureThemePositions(bool includeRenderTransform)
    {
        var positions = new Dictionary<ThemeDefinition, Point>();
        foreach (ThemeDefinition theme in ThemesList.Items)
        {
            if (ThemesList.ItemContainerGenerator.ContainerFromItem(theme) is not UIElement container)
                continue;

            Vector layoutOffset = VisualTreeHelper.GetOffset(container);
            Point position = new(layoutOffset.X, layoutOffset.Y);
            if (includeRenderTransform && container.RenderTransform is TranslateTransform transform)
                position.Offset(transform.X, transform.Y);
            positions[theme] = position;
        }

        return positions;
    }

    private void AnimateThemeReorder(IReadOnlyDictionary<ThemeDefinition, Point> positionsBeforeMove)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            Dictionary<ThemeDefinition, Point> positionsAfterMove = CaptureThemePositions(includeRenderTransform: false);
            IReadOnlyDictionary<ThemeDefinition, Vector> offsets =
                ThemeReorderAnimation.CalculateOffsets(positionsBeforeMove, positionsAfterMove);
            var easing = (IEasingFunction)FindResource("EaseOutEmphasized");

            foreach ((ThemeDefinition theme, Vector offset) in offsets)
            {
                if (ThemesList.ItemContainerGenerator.ContainerFromItem(theme) is not UIElement container)
                    continue;

                var slide = new TranslateTransform();
                container.RenderTransform = slide;
                slide.BeginAnimation(
                    TranslateTransform.XProperty,
                    new DoubleAnimation(offset.X, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
                slide.BeginAnimation(
                    TranslateTransform.YProperty,
                    new DoubleAnimation(offset.Y, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
            }
        }));
    }
}
