using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using InstanceManager.Controls;
using InstanceManager.Converters;
using Xunit;

namespace InstanceManager.Tests;

public sealed class GameGridLayoutTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(400, 1)]
    [InlineData(560, 2)]
    [InlineData(800, 3)]
    [InlineData(1100, 4)]
    [InlineData(1400, 5)]
    [InlineData(1640, 6)]
    [InlineData(5000, 8)]
    public void ColumnsForWidth_KeepsEveryCardAtLeast240Wide(double width, int expected)
    {
        int columns = GameGridColumnsConverter.ColumnsForWidth(width);

        Assert.Equal(expected, columns);
        if (width > 0 && columns < GameGridColumnsConverter.MaxColumns)
            Assert.True((width + GameGridColumnsConverter.Gap) / columns - GameGridColumnsConverter.Gap >= GameGridColumnsConverter.MinCardWidth);
    }

    [Theory]
    [InlineData(480d, 270d)]
    [InlineData(261.5, 147.09375)]
    [InlineData(1650d, 928.125)]
    public void AspectRatioBox_TakesItsHeightFromTheWidthItIsGiven(double width, double expectedHeight)
    {
        Exception? failure = RunOnStaThread(() =>
        {
            var box = new AspectRatioBox { Ratio = 0.5625, Child = new Border() };

            box.Measure(new Size(width, double.PositiveInfinity));
            Assert.Equal(width, box.DesiredSize.Width, 6);
            Assert.Equal(expectedHeight, box.DesiredSize.Height, 6);

            box.Arrange(new Rect(0, 0, width, expectedHeight * 3));
            Assert.Equal(expectedHeight, box.RenderSize.Height, 6);
            Assert.Equal(expectedHeight, ((Border)box.Child).RenderSize.Height, 6);
        });

        Assert.Null(failure);
    }

    [Fact]
    public void AspectRatioBox_ShrinksBackAfterBeingWide()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            var box = new AspectRatioBox { Ratio = 0.5625 };
            box.Measure(new Size(1650, double.PositiveInfinity));
            box.Measure(new Size(260, double.PositiveInfinity));

            Assert.Equal(146.25, box.DesiredSize.Height, 6);
        });

        Assert.Null(failure);
    }

    [Fact]
    public void GamesPage_ShowsWholeThumbnailsInAResponsiveGrid()
    {
        string xaml = Source("Views", "Pages", "GamesPage.xaml");

        Assert.Contains("Converter={StaticResource GameGridColumns}", xaml, StringComparison.Ordinal);
        Assert.Contains("<ui:AspectRatioBox Ratio=\"0.5625\">", xaml, StringComparison.Ordinal);
        Assert.Contains("<ItemsControl ItemsSource=\"{Binding Games}\" VerticalAlignment=\"Top\">", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AspectHeight", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Grid Height=\"128\">", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ThemeGridColumns", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void GameThumbnails_AreDecodedAtTheirFullSizeAndCached()
    {
        string games = Source("ViewModels", "GamesViewModel.cs");

        Assert.Contains("private const int ThumbnailWidth = 480;", games, StringComparison.Ordinal);
        Assert.Contains("DecodeFrozen(bytes, ThumbnailWidth)", games, StringComparison.Ordinal);
        Assert.Contains("_thumbnails.TryGetValue(info.ThumbnailUrl, out ImageSource? image)", games, StringComparison.Ordinal);
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

    private static string Source(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string path = Path.Combine(new[] { directory.FullName, "src", "InstanceManager" }.Concat(parts).ToArray());
            if (File.Exists(path))
                return File.ReadAllText(path);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(Path.Combine(parts));
    }
}
