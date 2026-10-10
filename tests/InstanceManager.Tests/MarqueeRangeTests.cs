using System;
using System.IO;
using InstanceManager.Behaviors;
using Xunit;

namespace InstanceManager.Tests;

public sealed class MarqueeRangeTests
{
    private static readonly ItemSpan[] Visible =
    {
        new(4, 0, 50), new(5, 50, 100), new(6, 100, 150), new(7, 150, 200)
    };

    [Fact]
    public void Locate_InsideAnItem_NamesThatItemFromBothSides() =>
        Assert.Equal(new MarqueeAnchor(5, 5), MarqueeRange.Locate(Visible, 70));

    [Fact]
    public void Locate_OnTheEdgeBetweenTwoItems_LeavesBothOut() =>
        Assert.Equal(new MarqueeAnchor(6, 5), MarqueeRange.Locate(Visible, 100));

    [Fact]
    public void Locate_AboveOrBelowEverythingOnScreen_PointsPastTheRealizedItems()
    {
        Assert.Equal(new MarqueeAnchor(4, 3), MarqueeRange.Locate(Visible, -20));
        Assert.Equal(new MarqueeAnchor(8, 7), MarqueeRange.Locate(Visible, 260));
    }

    [Fact]
    public void Locate_WithNothingOnScreen_SelectsNothing()
    {
        MarqueeAnchor anchor = MarqueeRange.Locate(Array.Empty<ItemSpan>(), 10);
        (int from, int to) = MarqueeRange.Between(anchor, anchor, currentIsBelow: true);

        Assert.True(from > to);
    }

    [Fact]
    public void Between_DraggingDown_CoversEveryItemTheBoxTouches()
    {
        MarqueeAnchor origin = MarqueeRange.Locate(Visible, 70);
        MarqueeAnchor current = MarqueeRange.Locate(Visible, 160);

        Assert.Equal((5, 7), MarqueeRange.Between(origin, current, currentIsBelow: true));
    }

    [Fact]
    public void Between_DraggingUp_CoversTheSameItems()
    {
        MarqueeAnchor origin = MarqueeRange.Locate(Visible, 160);
        MarqueeAnchor current = MarqueeRange.Locate(Visible, 70);

        Assert.Equal((5, 7), MarqueeRange.Between(origin, current, currentIsBelow: false));
    }

    [Fact]
    public void Between_KeepsItemsThatScrolledOutOfViewSinceTheDragStarted()
    {
        MarqueeAnchor origin = MarqueeRange.Locate(Visible, 70);
        var afterScrolling = new ItemSpan[] { new(20, 0, 50), new(21, 50, 100), new(22, 100, 150) };
        MarqueeAnchor current = MarqueeRange.Locate(afterScrolling, 120);

        Assert.Equal((5, 22), MarqueeRange.Between(origin, current, currentIsBelow: true));
    }

    [Fact]
    public void Between_ABoxInTheGapBelowTheLastItem_IsEmpty()
    {
        MarqueeAnchor origin = MarqueeRange.Locate(Visible, 230);
        MarqueeAnchor current = MarqueeRange.Locate(Visible, 260);
        (int from, int to) = MarqueeRange.Between(origin, current, currentIsBelow: true);

        Assert.True(from > to);
    }

    [Fact]
    public void AccountsPage_DrawsTheBoxOverTheList_AndDragsRowsByTheirNameOnly()
    {
        string xaml = Page("AccountsPage.xaml");
        string code = Page("AccountsPage.xaml.cs");

        Assert.Contains("x:Name=\"ListHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewMouseMove=\"ListHost_PreviewMouseMove\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"Marquee\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ListHost.CaptureMouse()", code, StringComparison.Ordinal);
        Assert.Contains("IsRowDragHandle(source)", code, StringComparison.Ordinal);
        Assert.Contains("AccountList.SelectOnly(", code, StringComparison.Ordinal);
    }

    private static string Page(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string path = Path.Combine(directory.FullName, "src", "InstanceManager", "Views", "Pages", fileName);
            if (File.Exists(path))
                return File.ReadAllText(path);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(fileName);
    }
}
