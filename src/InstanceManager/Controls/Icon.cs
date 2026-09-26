using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace InstanceManager.Controls;

public enum IconKind
{
    None,
    Users,
    UserPlus,
    Gamepad,
    Settings,
    Bell,
    Discord,
    Search,
    Close,
    Plus,
    Folder,
    FolderPlus,
    FolderMinus,
    FolderOpen,
    Play,
    Stop,
    More,
    Star,
    StarFilled,
    ChevronUp,
    ChevronDown,
    ChevronRight,
    Pencil,
    Trash,
    Check,
    Refresh,
    Layers,
    Info,
    AlertCircle,
    AlertTriangle,
    Minimize,
    Maximize,
    Restore
}

public sealed class Icon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(IconKind), typeof(Icon),
        new FrameworkPropertyMetadata(IconKind.None, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(Icon),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(Icon),
        new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(Icon),
        new FrameworkPropertyMetadata(SystemColors.ControlTextBrush,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    static Icon()
    {
        HorizontalAlignmentProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(HorizontalAlignment.Center));
        VerticalAlignmentProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(VerticalAlignment.Center));
    }

    public Icon()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    public IconKind Kind
    {
        get => (IconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize) => new(Size, Size);

    protected override void OnRender(DrawingContext dc)
    {
        if (!Shapes.TryGetValue(Kind, out IconShape shape))
            return;

        double scale = Math.Min(ActualWidth, ActualHeight) / 24;
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale,
            (ActualWidth - 24 * scale) / 2, (ActualHeight - 24 * scale) / 2));

        Brush brush = Foreground;
        Pen? pen = shape.Paint == Paint.Fill || StrokeThickness <= 0
            ? null
            : new Pen(brush, StrokeThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
        dc.DrawGeometry(shape.Paint == Paint.Stroke ? null : brush, pen, shape.Geometry);
        dc.Pop();
    }

    internal static Geometry? GeometryFor(IconKind kind) =>
        Shapes.TryGetValue(kind, out IconShape shape) ? shape.Geometry : null;

    private enum Paint { Stroke, Fill, FillAndStroke }

    private readonly record struct IconShape(Geometry Geometry, Paint Paint);

    private const string Circle12R10 = "M2 12a10 10 0 1 0 20 0a10 10 0 1 0-20 0";
    private const string FolderOutline =
        "M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-0.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2z";
    private const string Person = "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M5 7a4 4 0 1 0 8 0a4 4 0 1 0-8 0";
    private const string StarOutline =
        "M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01z";

    private static readonly Dictionary<IconKind, IconShape> Shapes = new()
    {
        [IconKind.Users] = S(Person + " M22 21v-2a4 4 0 0 0-3-3.87 M16 3.13a4 4 0 0 1 0 7.75"),
        [IconKind.UserPlus] = S(Person + " M19 8v6 M22 11h-6"),
        [IconKind.Gamepad] = S(
            "M6 11h4 M8 9v4 M15 12h0.01 M18 10h0.01 " +
            "M17.32 5H6.68a4 4 0 0 0-3.978 3.59C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-0.5 2-1l1.414-1.414" +
            "A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414 0.586L17 18c0.5 0.5 1 1 2 1a3 3 0 0 0 3-3" +
            "c0-1.545-0.604-6.584-0.685-7.258A4 4 0 0 0 17.32 5z"),
        [IconKind.Settings] = S(
            "M12.22 2h-0.44a2 2 0 0 0-2 2v0.18a2 2 0 0 1-1 1.73l-0.43 0.25a2 2 0 0 1-2 0l-0.15-0.08" +
            "a2 2 0 0 0-2.73 0.73l-0.22 0.38a2 2 0 0 0 0.73 2.73l0.15 0.1a2 2 0 0 1 1 1.72v0.51a2 2 0 0 1-1 1.74" +
            "l-0.15 0.09a2 2 0 0 0-0.73 2.73l0.22 0.38a2 2 0 0 0 2.73 0.73l0.15-0.08a2 2 0 0 1 2 0l0.43 0.25" +
            "a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h0.44a2 2 0 0 0 2-2v-0.18a2 2 0 0 1 1-1.73l0.43-0.25a2 2 0 0 1 2 0" +
            "l0.15 0.08a2 2 0 0 0 2.73-0.73l0.22-0.39a2 2 0 0 0-0.73-2.73l-0.15-0.08a2 2 0 0 1-1-1.74v-0.5" +
            "a2 2 0 0 1 1-1.74l0.15-0.09a2 2 0 0 0 0.73-2.73l-0.22-0.38a2 2 0 0 0-2.73-0.73l-0.15 0.08a2 2 0 0 1-2 0" +
            "l-0.43-0.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M9 12a3 3 0 1 0 6 0a3 3 0 1 0-6 0"),
        [IconKind.Bell] = S(
            "M10.268 21a2 2 0 0 0 3.464 0 " +
            "M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 0.74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8" +
            "c0 4.499-1.411 5.956-2.738 7.326"),
        [IconKind.Discord] = S(
            "M20.317 4.3698a19.7913 19.7913 0 0 0-4.8851-1.5152a0.0741 0.0741 0 0 0-0.0785 0.0371" +
            "c-0.211 0.3753-0.4447 0.8648-0.6083 1.2495c-1.8447-0.2762-3.68-0.2762-5.4868 0" +
            "c-0.1636-0.3933-0.4058-0.8742-0.6177-1.2495a0.077 0.077 0 0 0-0.0785-0.037" +
            "a19.7363 19.7363 0 0 0-4.8852 1.515a0.0699 0.0699 0 0 0-0.0321 0.0277" +
            "C0.5334 9.0458-0.319 13.5799 0.0992 18.0578a0.0824 0.0824 0 0 0 0.0312 0.0561" +
            "c2.0528 1.5076 4.0413 2.4228 5.9929 3.0294a0.0777 0.0777 0 0 0 0.0842-0.0276" +
            "c0.4616-0.6304 0.8731-1.2952 1.226-1.9942a0.076 0.076 0 0 0-0.0416-0.1057" +
            "c-0.6528-0.2476-1.2743-0.5495-1.8722-0.8923a0.077 0.077 0 0 1-0.0076-0.1277" +
            "c0.1258-0.0943 0.2517-0.1923 0.3718-0.2914a0.0743 0.0743 0 0 1 0.0776-0.0105" +
            "c3.9278 1.7933 8.18 1.7933 12.0614 0a0.0739 0.0739 0 0 1 0.0785 0.0095" +
            "c0.1202 0.099 0.246 0.1981 0.3728 0.2924a0.077 0.077 0 0 1-0.0066 0.1276" +
            "a12.2986 12.2986 0 0 1-1.873 0.8914a0.0766 0.0766 0 0 0-0.0407 0.1067" +
            "c0.3604 0.698 0.7719 1.3628 1.225 1.9932a0.076 0.076 0 0 0 0.0842 0.0286" +
            "c1.961-0.6067 3.9495-1.5219 6.0023-3.0294a0.077 0.077 0 0 0 0.0313-0.0552" +
            "c0.5004-5.177-0.8382-9.6739-3.5485-13.6604a0.061 0.061 0 0 0-0.0312-0.0286z " +
            "M8.02 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419c0-1.3332 0.9555-2.4189 2.157-2.4189" +
            "c1.2108 0 2.1757 1.0952 2.1568 2.419c0 1.3332-0.9555 2.4189-2.1569 2.4189z " +
            "M15.9948 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419c0-1.3332 0.9554-2.4189 2.1569-2.4189" +
            "c1.2108 0 2.1757 1.0952 2.1568 2.419c0 1.3332-0.946 2.4189-2.1568 2.4189z",
            Paint.Fill),
        [IconKind.Search] = S("M3 11a8 8 0 1 0 16 0a8 8 0 1 0-16 0 M21 21l-4.3-4.3"),
        [IconKind.Close] = S("M18 6L6 18 M6 6l12 12"),
        [IconKind.Plus] = S("M5 12h14 M12 5v14"),
        [IconKind.Folder] = S(FolderOutline),
        [IconKind.FolderPlus] = S(FolderOutline + " M12 10v6 M9 13h6"),
        [IconKind.FolderMinus] = S(FolderOutline + " M9 13h6"),
        [IconKind.FolderOpen] = S(
            "M6 14l1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5" +
            "a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69 0.9l0.81 1.2a2 2 0 0 0 1.67 0.9H18a2 2 0 0 1 2 2v2"),
        [IconKind.Play] = S("M7 4.5l12 7.5-12 7.5z", Paint.FillAndStroke),
        [IconKind.Stop] = S("M6.5 6.5h11v11h-11z", Paint.FillAndStroke),
        [IconKind.More] = S(
            "M4 12a1 1 0 1 0 2 0a1 1 0 1 0-2 0 M11 12a1 1 0 1 0 2 0a1 1 0 1 0-2 0 M18 12a1 1 0 1 0 2 0a1 1 0 1 0-2 0",
            Paint.FillAndStroke),
        [IconKind.Star] = S(StarOutline),
        [IconKind.StarFilled] = S(StarOutline, Paint.FillAndStroke),
        [IconKind.ChevronUp] = S("M18 15l-6-6-6 6"),
        [IconKind.ChevronDown] = S("M6 9l6 6 6-6"),
        [IconKind.ChevronRight] = S("M9 18l6-6-6-6"),
        [IconKind.Pencil] = S("M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5z M15 5l4 4"),
        [IconKind.Trash] = S(
            "M3 6h18 M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6 M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2 M10 11v6 M14 11v6"),
        [IconKind.Check] = S("M20 6L9 17l-5-5"),
        [IconKind.Refresh] = S(
            "M3 12a9 9 0 0 1 9-9a9.75 9.75 0 0 1 6.74 2.74L21 8 M21 3v5h-5 " +
            "M21 12a9 9 0 0 1-9 9a9.75 9.75 0 0 1-6.74-2.74L3 16 M8 16H3v5"),
        [IconKind.Layers] = S(
            "M12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9" +
            "a1 1 0 0 0 0-1.83z M2 12a1 1 0 0 0 0.58 0.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 12 " +
            "M2 17a1 1 0 0 0 0.58 0.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 17"),
        [IconKind.Info] = S(Circle12R10 + " M12 16v-4 M12 8h0.01"),
        [IconKind.AlertCircle] = S(Circle12R10 + " M12 8v4 M12 16h0.01"),
        [IconKind.AlertTriangle] = S(
            "M21.73 18l-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3z M12 9v4 M12 17h0.01"),
        [IconKind.Minimize] = S("M5 12h14"),
        [IconKind.Maximize] = S("M5 7a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2z"),
        [IconKind.Restore] = S(
            "M4 10a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2z " +
            "M8 6a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2")
    };

    private static IconShape S(string data, Paint paint = Paint.Stroke)
    {
        Geometry geometry = Geometry.Parse(data);
        geometry.Freeze();
        return new IconShape(geometry, paint);
    }
}
