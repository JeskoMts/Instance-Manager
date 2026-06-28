using System.Windows;

namespace InstanceManager.Behaviors;

public static class ThemeDrag
{
    public static readonly DependencyProperty IsDropTargetProperty =
        DependencyProperty.RegisterAttached(
            "IsDropTarget", typeof(bool), typeof(ThemeDrag), new PropertyMetadata(false));

    public static void SetIsDropTarget(DependencyObject element, bool value) =>
        element.SetValue(IsDropTargetProperty, value);

    public static bool GetIsDropTarget(DependencyObject element) =>
        (bool)element.GetValue(IsDropTargetProperty);
}
