using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace InstanceManager.Services;

internal readonly record struct RobloxProcessInfo(int Id, DateTime StartedUtc, bool HasVisibleWindow, string? CommandLine);

/// <summary>The operating-system side of <see cref="MultiInstanceManager"/>, separated so the policy can be tested.</summary>
internal interface IRobloxProcessHost
{
    IReadOnlyList<RobloxProcessInfo> List();

    IReadOnlyList<NamedHandleSweep> CloseSingletonHandles(IReadOnlyCollection<int> processIds);

    bool Terminate(int processId);
}

internal sealed class SystemRobloxProcessHost : IRobloxProcessHost
{
    internal const string PlayerProcessName = "RobloxPlayerBeta";

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ProcessCommandLineInformation = 60;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const int StatusBufferOverflow = unchecked((int)0x80000005);
    private const int StatusBufferTooSmall = unchecked((int)0xC0000023);

    public IReadOnlyList<RobloxProcessInfo> List()
    {
        Process[] processes = Process.GetProcessesByName(PlayerProcessName);
        var result = new List<RobloxProcessInfo>(processes.Length);
        foreach (Process process in processes)
        {
            using (process)
            {
                try
                {
                    if (process.HasExited)
                        continue;

                    DateTime started;
                    try { started = process.StartTime.ToUniversalTime(); }
                    catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { started = DateTime.MinValue; }

                    bool hasWindow;
                    try { hasWindow = process.MainWindowHandle != IntPtr.Zero; }
                    catch (InvalidOperationException) { continue; }

                    result.Add(new RobloxProcessInfo(process.Id, started, hasWindow, TryGetCommandLine(process.Id)));
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                }
            }
        }

        return result;
    }

    public IReadOnlyList<NamedHandleSweep> CloseSingletonHandles(IReadOnlyCollection<int> processIds) =>
        NamedObjectCloser.Close(processIds, MultiInstanceManager.SingletonNames);

    public bool Terminate(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!string.Equals(process.ProcessName, PlayerProcessName, StringComparison.OrdinalIgnoreCase))
                return true;

            process.Kill();
            return process.WaitForExit(5000);
        }
        catch (ArgumentException)
        {
            return true; // Already gone.
        }
        catch (InvalidOperationException)
        {
            return true; // Exited while we looked at it.
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    internal static string? TryGetCommandLine(int processId)
    {
        using SafeProcessHandle process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process.IsInvalid)
            return null;

        int size = 4096;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int status = NtQueryInformationProcess(process, ProcessCommandLineInformation, buffer, size, out int needed);
                if (status == StatusInfoLengthMismatch || status == StatusBufferOverflow || status == StatusBufferTooSmall)
                {
                    size = Math.Max(needed, size * 2);
                    continue;
                }

                if (status < 0)
                    return null;

                ushort length = (ushort)Marshal.ReadInt16(buffer);
                IntPtr text = Marshal.ReadIntPtr(buffer, IntPtr.Size);
                return text == IntPtr.Zero || length == 0 ? string.Empty : Marshal.PtrToStringUni(text, length / 2);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return null;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("ntdll.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int NtQueryInformationProcess(
        SafeProcessHandle process, int processInformationClass, IntPtr processInformation, int length, out int returnLength);
}
