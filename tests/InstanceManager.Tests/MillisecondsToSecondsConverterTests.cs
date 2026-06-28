using System.Globalization;
using InstanceManager.Converters;
using Xunit;

namespace InstanceManager.Tests;

public class MillisecondsToSecondsConverterTests
{
    [Theory]
    [InlineData(0.0, "Instant")]
    [InlineData(500.0, "0.5s")]
    [InlineData(1500.0, "1.5s")]
    [InlineData(3000.0, "3.0s")]
    [InlineData(5000.0, "5.0s")]
    public void Convert_FormatsMillisecondsAsSecondsLabel(double ms, string expected)
    {
        var converter = new MillisecondsToSecondsConverter();

        object result = converter.Convert(ms, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_AcceptsIntegerMilliseconds()
    {
        var converter = new MillisecondsToSecondsConverter();

        object result = converter.Convert(2500, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("2.5s", result);
    }
}
