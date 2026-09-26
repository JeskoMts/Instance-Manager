using System.Windows;
using InstanceManager.Controls;
using Xunit;

namespace InstanceManager.Tests;

public sealed class IconTests
{
    [Fact]
    public void EveryIconKind_HasAGeometryInsideTheGrid()
    {
        foreach (IconKind kind in Enum.GetValues<IconKind>().Where(k => k != IconKind.None))
        {
            Rect bounds = Icon.GeometryFor(kind)?.Bounds ?? Rect.Empty;

            Assert.False(bounds.IsEmpty, $"{kind} has no geometry.");
            Assert.True(bounds.Left >= -0.5 && bounds.Top >= -0.5 && bounds.Right <= 24.5 && bounds.Bottom <= 24.5,
                $"{kind} leaves the 24×24 grid: {bounds}.");
        }
    }
}
