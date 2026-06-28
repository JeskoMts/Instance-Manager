using InstanceManager.Converters;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ThemeGridColumnsConverterTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(559.99, 2)]
    [InlineData(560, 3)]
    [InlineData(799.99, 3)]
    [InlineData(800, 4)]
    [InlineData(1400, 4)]
    public void ColumnsForWidth_UsesApprovedResponsiveBreakpoints(double width, int expected)
    {
        Assert.Equal(expected, ThemeGridColumnsConverter.ColumnsForWidth(width));
    }
}
