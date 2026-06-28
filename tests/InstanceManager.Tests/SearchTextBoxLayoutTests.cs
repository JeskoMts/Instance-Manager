using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace InstanceManager.Tests;

[Collection("WPF application")]
public sealed class SearchTextBoxLayoutTests
{
    [Fact]
    public void CaretStartsAtSharedSearchInset()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            EnsureAppResources();

            var searchBox = new TextBox
            {
                Style = (Style)Application.Current!.FindResource("SearchTextBox"),
                Width = 360,
                Height = 36,
                Text = "x"
            };
            var host = new Window
            {
                Content = searchBox,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Width = 360,
                Height = 36,
                Left = -10_000,
                Top = -10_000
            };

            host.Show();
            try
            {
                searchBox.CaretIndex = 0;
                searchBox.UpdateLayout();

                Rect caret = searchBox.GetRectFromCharacterIndex(0, trailingEdge: false);
                Assert.False(caret.IsEmpty);
                Assert.InRange(caret.X, 34d, 38d);
            }
            finally
            {
                host.Close();
            }
        });

        Assert.Null(failure);
    }

    private static void EnsureAppResources()
    {
        if (Application.Current == null)
            _ = new Application();

        ResourceDictionary resources = Application.Current!.Resources;
        if (resources.MergedDictionaries.Count > 0)
            return;

        resources.MergedDictionaries.Add(new ResourceDictionary { Source = Pack("Themes/Colors.xaml") });
        resources.MergedDictionaries.Add(new ResourceDictionary { Source = Pack("Themes/Controls.xaml") });
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
