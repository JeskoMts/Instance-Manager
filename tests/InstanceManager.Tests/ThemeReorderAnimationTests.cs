using System.Windows;
using InstanceManager.Behaviors;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ThemeReorderAnimationTests
{
    [Fact]
    public void CalculateOffsets_MovingNonFirstItemBackward_TracksItemsByIdentity()
    {
        var before = new Dictionary<string, Point>
        {
            ["first"] = new(0, 0),
            ["dragged"] = new(100, 0),
            ["third"] = new(200, 0)
        };
        var after = new Dictionary<string, Point>
        {
            ["dragged"] = new(0, 0),
            ["first"] = new(100, 0),
            ["third"] = new(200, 0)
        };

        IReadOnlyDictionary<string, Vector> offsets = ThemeReorderAnimation.CalculateOffsets(before, after);

        Assert.Equal(new Vector(100, 0), offsets["dragged"]);
        Assert.Equal(new Vector(-100, 0), offsets["first"]);
        Assert.False(offsets.ContainsKey("third"));
    }

    [Fact]
    public void CalculateOffsets_MovingAcrossRows_UsesBothAxes()
    {
        var before = new Dictionary<string, Point> { ["dragged"] = new(100, 0) };
        var after = new Dictionary<string, Point> { ["dragged"] = new(0, 80) };

        IReadOnlyDictionary<string, Vector> offsets = ThemeReorderAnimation.CalculateOffsets(before, after);

        Assert.Equal(new Vector(100, -80), offsets["dragged"]);
    }
}
