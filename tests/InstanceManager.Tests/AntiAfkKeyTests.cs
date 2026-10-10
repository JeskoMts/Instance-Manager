using System.Windows.Input;
using InstanceManager.Models;
using Xunit;

namespace InstanceManager.Tests;

public sealed class AntiAfkKeyTests
{
    [Theory]
    [InlineData("Space", Key.Space)]
    [InlineData("W", Key.W)]
    [InlineData("F13", Key.F13)]
    [InlineData("Left", Key.Left)]
    [InlineData("LeftShift", Key.LeftShift)]
    [InlineData("Shift", Key.LeftShift)]
    public void SavedName_ResolvesToItsKey(string name, Key expected)
    {
        Assert.True(AntiAfkKeys.TryParse(name, out Key key));
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Nonsense")]
    [InlineData("65")]
    [InlineData("Space,W")]
    [InlineData("None")]
    [InlineData("System")]
    public void NameThatIsNoKey_IsRejected(string? name) =>
        Assert.False(AntiAfkKeys.TryParse(name, out _));

    [Theory]
    [InlineData(Key.Escape)]
    [InlineData(Key.LWin)]
    [InlineData(Key.RWin)]
    [InlineData(Key.Sleep)]
    [InlineData(Key.BrowserBack)]
    [InlineData(Key.BrowserHome)]
    [InlineData(Key.VolumeMute)]
    [InlineData(Key.MediaPlayPause)]
    [InlineData(Key.LaunchMail)]
    [InlineData(Key.LaunchApplication2)]
    [InlineData(Key.System)]
    [InlineData(Key.None)]
    public void KeyThatActsOutsideRoblox_IsNotAllowed(Key key)
    {
        Assert.False(AntiAfkKeys.IsAllowed(key));
        Assert.False(AntiAfkKeys.TryParse(key.ToString(), out _));
    }

    [Theory]
    [InlineData(Key.Space)]
    [InlineData(Key.W)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.OemSemicolon)]
    [InlineData(Key.Tab)]
    [InlineData(Key.Return)]
    [InlineData(Key.Up)]
    [InlineData(Key.F13)]
    [InlineData(Key.D1)]
    [InlineData(Key.NumPad0)]
    [InlineData(Key.OemPlus)]
    [InlineData(Key.CapsLock)]
    public void OrdinaryKey_IsAllowed(Key key) => Assert.True(AntiAfkKeys.IsAllowed(key));

    [Theory]
    [InlineData("F13", "F13")]
    [InlineData("Enter", "Enter")]
    [InlineData("Shift", "LeftShift")]
    [InlineData(null, "Space")]
    [InlineData("Escape", "Space")]
    [InlineData("Nonsense", "Space")]
    public void Normalize_KeepsAKeyRenamesTheOldShiftAndRepairsTheRest(string? saved, string expected) =>
        Assert.Equal(expected, AntiAfkKeys.Normalize(saved));

    [Theory]
    [InlineData("Space", "Space")]
    [InlineData("W", "W")]
    [InlineData("F13", "F13")]
    [InlineData("LeftShift", "Left Shift")]
    [InlineData("Shift", "Left Shift")]
    [InlineData("D1", "1")]
    [InlineData("NumPad5", "Num 5")]
    [InlineData("Return", "Enter")]
    [InlineData("Next", "Page Down")]
    [InlineData("PageDown", "Page Down")]
    [InlineData("Nonsense", "Space")]
    public void Format_ShowsWhatIsPrintedOnTheKey(string saved, string expected) =>
        Assert.Equal(expected, AntiAfkKeys.Format(saved));

    [Theory]
    [InlineData("Space", 0x20, false)]
    [InlineData("W", 0x57, false)]
    [InlineData("LeftShift", 0xA0, false)]
    [InlineData("Shift", 0xA0, false)]
    [InlineData("F13", 0x7C, false)]
    [InlineData("Return", 0x0D, false)]
    [InlineData("NumPad5", 0x65, false)]
    [InlineData("Left", 0x25, true)]
    [InlineData("Delete", 0x2E, true)]
    [InlineData("RightCtrl", 0xA3, true)]
    public void VirtualKey_IsTheOneWindowsUsesAndSaysWhetherItIsExtended(string saved, int virtualKey, bool extended)
    {
        Assert.True(AntiAfkKeys.TryGetVirtualKey(saved, out byte actualKey, out bool actualExtended));
        Assert.Equal(virtualKey, actualKey);
        Assert.Equal(extended, actualExtended);
    }

    [Fact]
    public void VirtualKey_OfANameThatIsNoKey_IsNotFound() =>
        Assert.False(AntiAfkKeys.TryGetVirtualKey("Nonsense", out _, out _));

    [Fact]
    public void Settings_KeepACustomKey()
    {
        var settings = new AppSettings { AntiAfkKey = "F13" };

        Assert.False(settings.Normalize());
        Assert.Equal("F13", settings.AntiAfkKey);
    }

    [Theory]
    [InlineData("Shift", "LeftShift")]
    [InlineData("Escape", "Space")]
    [InlineData("Nonsense", "Space")]
    public void Settings_RepairAKeyThatCannotBePressed(string saved, string repaired)
    {
        var settings = new AppSettings { AntiAfkKey = saved };

        Assert.True(settings.Normalize());
        Assert.Equal(repaired, settings.AntiAfkKey);
    }
}
