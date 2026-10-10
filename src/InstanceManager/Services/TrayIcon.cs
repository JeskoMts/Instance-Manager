using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace InstanceManager.Services;

public sealed class TrayIcon : IDisposable
{
    private const int CallbackMessage = 0x8000 + 17;
    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;
    private const uint NimAdd = 0;
    private const uint NimModify = 1;
    private const uint NimDelete = 2;
    private const uint NifMessage = 0x1;
    private const uint NifIcon = 0x2;
    private const uint NifTip = 0x4;

    private readonly HwndSource _window;
    private readonly uint _taskbarCreated;
    private readonly IntPtr _icon;
    private readonly bool _ownsIcon;
    private string _tooltip;
    private bool _visible;
    private bool _disposed;

    public TrayIcon(string tooltip)
    {
        _tooltip = tooltip;
        _window = new HwndSource(new HwndSourceParameters("InstanceManagerTray") { Width = 0, Height = 0, WindowStyle = 0 });
        _window.AddHook(WndProc);
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        (_icon, _ownsIcon) = LoadAppIcon();
    }

    public event Action? Activated;

    public event Action? MenuRequested;

    public IntPtr Handle => _window.Handle;

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_disposed || value == _visible)
                return;
            _visible = value;
            Notify(value ? NimAdd : NimDelete);
        }
    }

    public string Tooltip
    {
        get => _tooltip;
        set
        {
            _tooltip = value;
            if (_visible)
                Notify(NimModify);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Visible = false;
        _disposed = true;
        _window.RemoveHook(WndProc);
        _window.Dispose();
        if (_ownsIcon && _icon != IntPtr.Zero)
            DestroyIcon(_icon);
    }

    private void Notify(uint message)
    {
        var data = new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = _window.Handle,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon,
            szTip = _tooltip.Length > 127 ? _tooltip[..127] : _tooltip
        };
        Shell_NotifyIcon(message, ref data);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == CallbackMessage)
        {
            int mouse = lParam.ToInt32() & 0xFFFF;
            if (mouse is WmLButtonUp or WmLButtonDblClk)
                Activated?.Invoke();
            else if (mouse == WmRButtonUp)
                MenuRequested?.Invoke();
            handled = true;
        }
        else if (msg == _taskbarCreated && _visible)
        {
            Notify(NimAdd);
        }
        return IntPtr.Zero;
    }

    private static (IntPtr Icon, bool Owned) LoadAppIcon()
    {
        string? path = Environment.ProcessPath;
        if (path != null)
        {
            var small = new IntPtr[1];
            if (ExtractIconEx(path, 0, null, small, 1) > 0 && small[0] != IntPtr.Zero)
                return (small[0], true);
        }
        return (LoadIcon(IntPtr.Zero, new IntPtr(32512)), false);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadIconW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr name);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterWindowMessageW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RegisterWindowMessage(string name);
}
