using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Storage;

namespace InstanceManager.Services;

public sealed class AntiAfkService : IDisposable
{
    private const uint KeyEventExtended = 0x1;
    private const uint KeyEventKeyUp = 0x2;
    private const uint MapVirtualKeyToScanCode = 0;

    private readonly InstanceTracker _tracker;
    private readonly ISettingsService _settings;
    private CancellationTokenSource? _stop;

    public AntiAfkService(InstanceTracker tracker, ISettingsService settings)
    {
        _tracker = tracker;
        _settings = settings;
    }

    public void Restart()
    {
        Stop();
        if (!_settings.Settings.AntiAfkEnabled) return;
        _stop = new CancellationTokenSource();
        _ = RunAsync(_stop.Token);
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(_settings.Settings.AntiAfkIntervalMinutes), token);
                await SendNowAsync(token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async Task<int> SendNowAsync(CancellationToken token = default)
    {
        IntPtr previous = GetForegroundWindow();
        int sent = 0;
        if (!AntiAfkKeys.TryGetVirtualKey(_settings.Settings.AntiAfkKey, out byte key, out bool extended))
            AntiAfkKeys.TryGetVirtualKey(AntiAfkKeys.Default, out key, out extended);
        byte scanCode = (byte)MapVirtualKey(key, MapVirtualKeyToScanCode);
        uint flags = extended ? KeyEventExtended : 0;
        try
        {
            foreach (Process process in _tracker.RunningProcesses)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    process.Refresh();
                    IntPtr handle = process.MainWindowHandle;
                    if (handle == IntPtr.Zero || !FocusWindow(handle)) continue;
                    await Task.Delay(80, token);
                    if (GetForegroundWindow() != handle) continue;
                    keybd_event(key, scanCode, flags, UIntPtr.Zero);
                    try { await Task.Delay(60, token); sent++; }
                    finally { keybd_event(key, scanCode, flags | KeyEventKeyUp, UIntPtr.Zero); }
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
        }
        finally
        {
            if (_settings.Settings.AntiAfkReturnFocus && previous != IntPtr.Zero)
                FocusWindow(previous);
        }
        return sent;
    }

    public void Stop() { _stop?.Cancel(); _stop?.Dispose(); _stop = null; }
    public void Dispose() => Stop();

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachInput);
    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")] private static extern uint MapVirtualKey(uint code, uint mapType);

    private static bool FocusWindow(IntPtr handle)
    {
        if (SetForegroundWindow(handle)) return true;
        IntPtr current = GetForegroundWindow();
        uint foregroundThread = GetWindowThreadProcessId(current, IntPtr.Zero);
        uint ownThread = GetCurrentThreadId();
        if (foregroundThread == 0 || foregroundThread == ownThread || !AttachThreadInput(ownThread, foregroundThread, true))
            return false;
        try { return SetForegroundWindow(handle); }
        finally { AttachThreadInput(ownThread, foregroundThread, false); }
    }
}
