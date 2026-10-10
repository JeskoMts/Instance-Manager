using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

    public static Image CreateGhost(FrameworkElement source)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(source);
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(source.ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(source.ActualHeight * dpi.DpiScaleY)),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);

        var bounds = new Rect(0, 0, source.ActualWidth, source.ActualHeight);
        var snapshot = new DrawingVisual();
        using (DrawingContext dc = snapshot.RenderOpen())
        {
            dc.DrawRectangle(new VisualBrush(source), null, bounds);
        }
        bitmap.Render(snapshot);
        bitmap.Freeze();
        return new Image
        {
            Source = bitmap,
            Width = source.ActualWidth,
            Height = source.ActualHeight,
            Opacity = 0.8,
            IsHitTestVisible = false
        };
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
