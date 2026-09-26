using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using InstanceManager.Behaviors;
using Xunit;

namespace InstanceManager.Tests;

[Collection("WPF application")]
public sealed class ControlBehaviorTests
{
    [Fact]
    public void SliderPressDrag_SnapsToTicksAndStaysInRange()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            var slider = new Slider { Minimum = 0, Maximum = 30000, TickFrequency = 500, IsSnapToTickEnabled = true };

            Assert.Equal(11000, SliderPressDrag.Snap(slider, 10_840));
            Assert.Equal(10500, SliderPressDrag.Snap(slider, 10_700));
            Assert.Equal(0, SliderPressDrag.Snap(slider, -250));
            Assert.Equal(30000, SliderPressDrag.Snap(slider, 31_000));

            slider.IsSnapToTickEnabled = false;
            Assert.Equal(10_840, SliderPressDrag.Snap(slider, 10_840));
        });

        Assert.Null(failure);
    }

    [Fact]
    public void SliderPressDrag_MapsPointerToValueAbsolutely()
    {
        Assert.Equal(0, SliderPressDrag.ValueAt(14, 700, 28, 0, 30000));
        Assert.Equal(15000, SliderPressDrag.ValueAt(350, 700, 28, 0, 30000));
        Assert.Equal(30000, SliderPressDrag.ValueAt(686, 700, 28, 0, 30000));
        Assert.Equal(0, SliderPressDrag.ValueAt(-50, 700, 28, 0, 30000));
        Assert.Equal(30000, SliderPressDrag.ValueAt(900, 700, 28, 0, 30000));
        Assert.Equal(1, SliderPressDrag.ValueAt(100, 20, 28, 1, 31));
    }

    [Fact]
    public void DragGhost_CapturesItemsThatAreNotAtTheOriginOfTheirPanel()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            var first = new Border { Width = 100, Height = 50, Background = System.Windows.Media.Brushes.Blue };
            var second = new Border { Width = 100, Height = 50, Background = System.Windows.Media.Brushes.Red };
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(first);
            panel.Children.Add(second);
            panel.Measure(new Size(300, 100));
            panel.Arrange(new Rect(0, 0, 300, 100));

            var ghost = (System.Windows.Media.Imaging.BitmapSource)MainWindow.CreateDragGhost(second).Source;
            var pixel = new byte[4];
            ghost.CopyPixels(new Int32Rect(50, 25, 1, 1), pixel, 4, 0);

            Assert.Equal(new byte[] { 0, 0, 255, 255 }, pixel);
        });

        Assert.Null(failure);
    }

    [Fact]
    public void RowCheckBox_ShowsPartialFill_EvenAfterHavingBeenChecked()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            var host = new Window
            {
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Width = 40,
                Height = 40,
                Left = -10_000,
                Top = -10_000
            };
            _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
            host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = Pack("Themes/Colors.xaml") });
            host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = Pack("Themes/Controls.xaml") });
            var box = new CheckBox { Style = (Style)host.FindResource("RowCheckBox") };
            host.Content = box;

            host.Show();
            try
            {
                box.IsChecked = true;
                Pump(300);
                box.IsChecked = false;
                Pump(300);
                box.IsChecked = null;
                Pump(300);

                var fill = (FrameworkElement)box.Template.FindName("Fill", box);
                var dash = (FrameworkElement)box.Template.FindName("Dash", box);
                Assert.Equal(1.0, fill.Opacity);
                Assert.Equal(Visibility.Visible, dash.Visibility);
            }
            finally
            {
                host.Close();
            }
        });

        Assert.Null(failure);
    }

    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static Uri Pack(string relative) =>
        new($"pack://application:,,,/InstanceManager;component/{relative}", UriKind.Absolute);

    private static Exception? RunOnStaThread(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return captured;
    }
}
