using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using InstanceManager.Controls;
using Xunit;

namespace InstanceManager.Tests;

public sealed class DeferredContentTests
{
    [Fact]
    public void Content_IsBuiltOnlyWhenAskedForAndThenKept()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            var host = new DeferredContent { ContentType = typeof(Border) };
            FrameworkElement? realizedEvent = null;
            host.Realized += element => realizedEvent = element;

            Assert.False(host.IsRealized);
            Assert.Null(host.Content);

            host.Visibility = Visibility.Collapsed;
            host.Visibility = Visibility.Visible;
            Assert.False(host.IsRealized);

            FrameworkElement? first = host.Realize();
            FrameworkElement? second = host.Realize();

            Assert.IsType<Border>(first);
            Assert.Same(first, second);
            Assert.Same(first, host.Content);
            Assert.Same(first, realizedEvent);
        });

        Assert.Null(failure);
    }

    [Fact]
    public void Content_IsBuiltWhenAShownWindowMakesItVisible()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            var host = new DeferredContent { ContentType = typeof(Border), Visibility = Visibility.Collapsed };
            var window = new Window
            {
                Content = host, Width = 200, Height = 200, Left = -10000, Top = -10000,
                ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual
            };
            try
            {
                window.Show();
                Assert.True(host.IsLoaded);
                Assert.False(host.IsRealized);

                host.Visibility = Visibility.Visible;
                Assert.True(host.IsRealized);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Null(failure);
    }

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
