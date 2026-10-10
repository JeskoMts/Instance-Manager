using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InstanceManager.Controls;

public partial class ColorPicker : UserControl
{
    public static readonly DependencyProperty HexProperty = DependencyProperty.Register(
        nameof(Hex), typeof(string), typeof(ColorPicker),
        new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHexChanged));

    private double _hue;
    private double _saturation;
    private double _value = 1;
    private bool _syncing;

    public ColorPicker()
    {
        InitializeComponent();
        SvArea.SizeChanged += (_, _) => PlaceThumbs();
        HueBar.SizeChanged += (_, _) => PlaceThumbs();
        Loaded += (_, _) => LoadFromHex(Hex, updateText: true);
    }

    public string Hex
    {
        get => (string)GetValue(HexProperty);
        set => SetValue(HexProperty, value);
    }

    public Color SelectedColor => ColorMath.FromHsv(_hue, _saturation, _value);

    private static void OnHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (ColorPicker)d;
        if (!picker._syncing)
            picker.LoadFromHex(e.NewValue as string, updateText: !picker.HexBox.IsKeyboardFocused);
    }

    private void LoadFromHex(string? hex, bool updateText)
    {
        if (!ColorMath.TryParseHex(hex, out Color color))
            return;

        (double hue, double saturation, double value) = ColorMath.ToHsv(color);
        if (saturation > 0 && value > 0)
            _hue = hue;
        if (value > 0)
            _saturation = saturation;
        _value = value;
        Render(updateText);
    }

    private void Render(bool updateText)
    {
        Color color = SelectedColor;
        HueFill.Background = new SolidColorBrush(ColorMath.FromHsv(_hue, 1, 1));
        Preview.Background = new SolidColorBrush(color);
        if (updateText)
        {
            _syncing = true;
            HexBox.Text = ColorMath.ToHex(color)[1..];
            _syncing = false;
        }
        PlaceThumbs();
    }

    private void PlaceThumbs()
    {
        double width = SvArea.ActualWidth;
        double height = SvArea.ActualHeight;
        Canvas.SetLeft(SvThumb, _saturation * width - SvThumb.Width / 2);
        Canvas.SetTop(SvThumb, (1 - _value) * height - SvThumb.Height / 2);
        Canvas.SetLeft(HueThumb, _hue / 360 * HueBar.ActualWidth - HueThumb.Width / 2);
        Canvas.SetTop(HueThumb, 0);
    }

    private void Commit()
    {
        _syncing = true;
        Hex = ColorMath.ToHex(SelectedColor);
        _syncing = false;
    }

    private void SvArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SvArea.CaptureMouse();
        UpdateFromSv(e.GetPosition(SvArea));
        e.Handled = true;
    }

    private void SvArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (SvArea.IsMouseCaptured)
            UpdateFromSv(e.GetPosition(SvArea));
    }

    private void HueBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HueBar.CaptureMouse();
        UpdateFromHue(e.GetPosition(HueBar));
        e.Handled = true;
    }

    private void HueBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (HueBar.IsMouseCaptured)
            UpdateFromHue(e.GetPosition(HueBar));
    }

    private void Area_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element && element.IsMouseCaptured)
            element.ReleaseMouseCapture();
    }

    private void Area_LostMouseCapture(object sender, MouseEventArgs e) => Commit();

    private void UpdateFromSv(Point point)
    {
        _saturation = Math.Clamp(point.X / Math.Max(1, SvArea.ActualWidth), 0, 1);
        _value = 1 - Math.Clamp(point.Y / Math.Max(1, SvArea.ActualHeight), 0, 1);
        Render(updateText: true);
        Commit();
    }

    private void UpdateFromHue(Point point)
    {
        _hue = Math.Clamp(point.X / Math.Max(1, HueBar.ActualWidth), 0, 1) * 359.999;
        Render(updateText: true);
        Commit();
    }

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || !ColorMath.TryParseHex(HexBox.Text, out Color color) || HexBox.Text.TrimStart('#').Length != 6)
            return;

        (double hue, double saturation, double value) = ColorMath.ToHsv(color);
        if (saturation > 0 && value > 0)
            _hue = hue;
        _saturation = saturation;
        _value = value;
        Render(updateText: false);
        Commit();
    }

    private void HexBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Render(updateText: true);

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Render(updateText: true);
            e.Handled = true;
        }
    }
}
