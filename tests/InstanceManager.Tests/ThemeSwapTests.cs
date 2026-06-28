using InstanceManager.Behaviors;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ThemeSwapTests
{
    [Fact]
    public void SwapPair_ExchangesOnlyTheTwoEndpoints_IntermediatesKeepTheirSlots()
    {
        object a = new(), b = new(), c = new(), d = new(), e = new();
        var baseOrder = new[] { a, b, c, d, e };

        var result = ThemeSwap.SwapPair(baseOrder, a, e);

        Assert.Equal(new[] { e, b, c, d, a }, result);
    }

    [Fact]
    public void SwapPair_AdjacentEndpoints_Swaps()
    {
        object a = new(), b = new(), c = new();

        Assert.Equal(new[] { a, c, b }, ThemeSwap.SwapPair(new[] { a, b, c }, b, c));
    }

    [Fact]
    public void SwapPair_IsComputedFromTheBaseOrder_NotProgressively()
    {
        object a = new(), b = new(), c = new(), d = new();
        var baseOrder = new[] { a, b, c, d };

        Assert.Equal(new[] { c, b, a, d }, ThemeSwap.SwapPair(baseOrder, a, c));
        Assert.Equal(new[] { d, b, c, a }, ThemeSwap.SwapPair(baseOrder, a, d));
    }

    [Fact]
    public void SwapPair_SameOrMissingItem_ReturnsAnUnchangedCopy()
    {
        object a = new(), b = new(), z = new();
        var baseOrder = new[] { a, b };

        Assert.Equal(new[] { a, b }, ThemeSwap.SwapPair(baseOrder, a, a));
        Assert.Equal(new[] { a, b }, ThemeSwap.SwapPair(baseOrder, a, z));
    }
}
