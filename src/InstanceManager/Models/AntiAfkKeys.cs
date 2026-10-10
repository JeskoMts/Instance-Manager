using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace InstanceManager.Models;

public static class AntiAfkKeys
{
    public const string Default = "Space";

    private const string LegacyShift = "Shift";

    private static readonly Dictionary<string, Key> Names = Enum.GetNames<Key>()
        .ToDictionary(name => name, name => Enum.Parse<Key>(name), StringComparer.Ordinal);

    public static bool TryParse(string? name, out Key key)
    {
        if (name == LegacyShift)
            name = nameof(Key.LeftShift);
        if (name != null && Names.TryGetValue(name, out Key found) && IsAllowed(found))
        {
            key = found;
            return true;
        }

        key = Key.None;
        return false;
    }

    public static bool IsAllowed(Key key) =>
        KeyInterop.VirtualKeyFromKey(key) != 0
        && key is not (Key.System or Key.ImeProcessed or Key.DeadCharProcessed
            or Key.Escape or Key.LWin or Key.RWin or Key.Sleep)
        && key is not (>= Key.BrowserBack and <= Key.LaunchApplication2);

    public static string Normalize(string? name)
    {
        if (name == LegacyShift)
            return nameof(Key.LeftShift);
        return name != null && TryParse(name, out _) ? name : Default;
    }

    public static string Format(string? name) => Label(TryParse(name, out Key key) ? key : Key.Space);

    public static bool TryGetVirtualKey(string? name, out byte virtualKey, out bool extended)
    {
        virtualKey = 0;
        extended = false;
        if (!TryParse(name, out Key key))
            return false;

        int code = KeyInterop.VirtualKeyFromKey(key);
        virtualKey = (byte)code;
        extended = IsExtended(code);
        return true;
    }

    private static bool IsExtended(int virtualKey) => virtualKey is
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28
        or 0x2C or 0x2D or 0x2E
        or 0x5D
        or 0x6F
        or 0x90
        or 0xA3 or 0xA5;

    private static string Label(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (char)('0' + (key - Key.NumPad0)),
        Key.OemPlus => "+",
        Key.OemMinus => "-",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.Return => "Enter",
        Key.Next => "Page Down",
        Key.Prior => "Page Up",
        Key.Capital => "Caps Lock",
        Key.Back => "Backspace",
        Key.Snapshot => "Print Screen",
        _ => SplitWords(key.ToString())
    };

    private static string SplitWords(string name)
    {
        var text = new StringBuilder(name.Length + 2);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                text.Append(' ');
            text.Append(name[i]);
        }

        return text.ToString();
    }
}
