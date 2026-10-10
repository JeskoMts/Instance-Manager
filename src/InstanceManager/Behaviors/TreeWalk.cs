using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace InstanceManager.Behaviors;

public static class TreeWalk
{
    public static bool IsDescendantOrSelf(DependencyObject? source, DependencyObject ancestor)
    {
        DependencyObject? node = source;
        while (node != null)
        {
            if (ReferenceEquals(node, ancestor))
                return true;
            node = Parent(node);
        }
        return false;
    }

    public static bool IsInteractive(DependencyObject? source)
    {
        DependencyObject? node = source;
        while (node != null)
        {
            if (node is ButtonBase or ComboBox or TextBoxBase)
                return true;
            node = Parent(node);
        }
        return false;
    }

    private static DependencyObject? Parent(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);
}
