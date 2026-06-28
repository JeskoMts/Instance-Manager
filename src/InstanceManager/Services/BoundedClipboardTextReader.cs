using System;
using System.Runtime.InteropServices;

namespace InstanceManager.Services;

internal static class BoundedClipboardTextReader
{
    private const uint UnicodeTextFormat = 13;

    public static bool TryRead(int maxCharacters, out string value)
    {
        value = string.Empty;
        if (!OperatingSystem.IsWindows() || maxCharacters <= 0 || !OpenClipboard(IntPtr.Zero))
            return false;

        IntPtr locked = IntPtr.Zero;
        try
        {
            if (!IsClipboardFormatAvailable(UnicodeTextFormat))
                return false;

            IntPtr handle = GetClipboardData(UnicodeTextFormat);
            if (handle == IntPtr.Zero)
                return false;

            UIntPtr byteSize = GlobalSize(handle);
            if (!IsUnicodeTextSizeAllowed(byteSize, maxCharacters))
                return false;

            locked = GlobalLock(handle);
            if (locked == IntPtr.Zero)
                return false;

            int characterCapacity = checked((int)(byteSize.ToUInt64() / sizeof(char)));
            string raw = Marshal.PtrToStringUni(locked, characterCapacity) ?? string.Empty;
            int terminator = raw.IndexOf('\0');
            value = terminator >= 0 ? raw[..terminator] : raw;
            if (value.Length > maxCharacters)
            {
                value = string.Empty;
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentException)
        {
            value = string.Empty;
            return false;
        }
        finally
        {
            if (locked != IntPtr.Zero)
                GlobalUnlock(locked);
            CloseClipboard();
        }
    }

    internal static bool IsUnicodeTextSizeAllowed(UIntPtr byteSize, int maxCharacters)
    {
        if (maxCharacters <= 0)
            return false;

        ulong maxBytes = checked((ulong)maxCharacters * sizeof(char));
        return byteSize.ToUInt64() <= maxBytes;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr newOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern UIntPtr GlobalSize(IntPtr memory);
}
