using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace InstanceManager.Behaviors;

public sealed class DragAdorner : Adorner
{
    private readonly UIElement _ghost;
    private Point _position;

    public DragAdorner(UIElement adornedElement, UIElement ghost) : base(adornedElement)
    {
        _ghost = ghost;
        IsHitTestVisible = false;
        AddVisualChild(_ghost);
    }

    public void SetPosition(Point position)
    {
        _position = position;
        InvalidateArrange();
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _ghost;

    protected override Size MeasureOverride(Size constraint)
    {
        _ghost.Measure(constraint);
        return _ghost.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _ghost.Arrange(new Rect(_position, _ghost.DesiredSize));
        return finalSize;
    }
}
