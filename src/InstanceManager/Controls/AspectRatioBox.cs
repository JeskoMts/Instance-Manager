using System.Windows;
using System.Windows.Controls;

namespace InstanceManager.Controls;

public sealed class AspectRatioBox : Decorator
{
    public static readonly DependencyProperty RatioProperty = DependencyProperty.Register(
        nameof(Ratio), typeof(double), typeof(AspectRatioBox),
        new FrameworkPropertyMetadata(0.5625, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Ratio
    {
        get => (double)GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        Size size = SizeFor(constraint.Width);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Size size = SizeFor(arrangeSize.Width);
        Child?.Arrange(new Rect(size));
        return size;
    }

    private Size SizeFor(double width)
    {
        double w = double.IsFinite(width) && width > 0 ? width : 0;
        return new Size(w, w * Ratio);
    }
}
