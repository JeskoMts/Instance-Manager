using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace InstanceManager.Services;

/// <summary>Outcome of closing named handles inside one process.</summary>
/// <param name="ProcessId">The process that was inspected.</param>
/// <param name="Closed">How many matching handles were closed inside it.</param>
/// <param name="OpenError">0 when the process could be inspected, otherwise the Win32 error from OpenProcess.</param>
internal readonly record struct NamedHandleSweep(int ProcessId, int Closed, int OpenError)
{
    public bool Blocked => OpenError != 0;
}

/// <summary>
/// Closes handles to named Event and Mutant objects inside other processes of the current user.
/// When the last handle to a named object closes, Windows removes the name, so the next process
/// that creates an object with that name gets a brand new one.
/// </summary>
internal static class NamedObjectCloser
{
    private const int SystemExtendedHandleInformation = 64;
    private const int ObjectNameInformation = 1;
    private const int ObjectTypeInformation = 2;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const int StatusBufferOverflow = unchecked((int)0x80000005);
    private const int StatusBufferTooSmall = unchecked((int)0xC0000023);
    private const uint ProcessDupHandle = 0x0040;
    private const uint DuplicateCloseSource = 0x1;
    private const uint DuplicateSameAccess = 0x2;
    private const int ErrorInvalidParameter = 87;
    private const int MaxSnapshotBytes = 512 * 1024 * 1024;

    private static readonly int EntrySize = Marshal.SizeOf<HandleEntry>();
    private static readonly int EntriesOffset = IntPtr.Size * 2;

    /// <summary>
    /// Closes every handle in the given processes whose object is an Event or Mutant named exactly
    /// one of <paramref name="objectNames"/> (compared against the last segment of the kernel path,
    /// ignoring case). Processes that exited in the meantime are reported with nothing closed.
    /// </summary>
    public static IReadOnlyList<NamedHandleSweep> Close(IReadOnlyCollection<int> processIds, IReadOnlyCollection<string> objectNames)
    {
        ArgumentNullException.ThrowIfNull(processIds);
        ArgumentNullException.ThrowIfNull(objectNames);

        var results = new List<NamedHandleSweep>(processIds.Count);
        if (processIds.Count == 0 || objectNames.Count == 0)
            return results;

        var wanted = new HashSet<string>(objectNames, StringComparer.OrdinalIgnoreCase);
        var targets = new HashSet<int>(processIds);
        Dictionary<int, List<IntPtr>> handlesByProcess = SnapshotHandles(targets);

        foreach (int pid in targets)
        {
            using SafeProcessHandle process = OpenProcess(ProcessDupHandle, false, pid);
            if (process.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                // ERROR_INVALID_PARAMETER means the process is gone, which is as good as clean.
                results.Add(new NamedHandleSweep(pid, 0, error == ErrorInvalidParameter ? 0 : error));
                continue;
            }

            int closed = 0;
            if (handlesByProcess.TryGetValue(pid, out List<IntPtr>? handles))
            {
                foreach (IntPtr handle in handles)
                {
                    if (CloseIfNamed(process, handle, wanted))
                        closed++;
                }
            }

            results.Add(new NamedHandleSweep(pid, closed, 0));
        }

        return results;
    }

    private static bool CloseIfNamed(SafeProcessHandle process, IntPtr remoteHandle, HashSet<string> wanted)
    {
        IntPtr self = GetCurrentProcess();
        if (!DuplicateHandle(process, remoteHandle, self, out IntPtr local, 0, false, DuplicateSameAccess))
            return false;

        bool matches;
        try
        {
            string? type = QueryUnicodeString(local, ObjectTypeInformation);
            if (!string.Equals(type, "Event", StringComparison.Ordinal) &&
                !string.Equals(type, "Mutant", StringComparison.Ordinal))
                return false;

            // Querying names is only safe for object types that can't block (Event, Mutant).
            string? name = QueryUnicodeString(local, ObjectNameInformation);
            matches = name != null && wanted.Contains(LastSegment(name));
        }
        finally
        {
            CloseHandle(local);
        }

        if (!matches)
            return false;

        if (!DuplicateHandle(process, remoteHandle, self, out IntPtr closedCopy, 0, false, DuplicateCloseSource))
            return false;

        if (closedCopy != IntPtr.Zero)
            CloseHandle(closedCopy);
        return true;
    }

    internal static string LastSegment(string kernelPath)
    {
        int slash = kernelPath.LastIndexOf('\\');
        return slash >= 0 ? kernelPath[(slash + 1)..] : kernelPath;
    }

    private static Dictionary<int, List<IntPtr>> SnapshotHandles(HashSet<int> targets)
    {
        var byProcess = new Dictionary<int, List<IntPtr>>();
        int size = 1024 * 1024;
        while (true)
        {
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int status = NtQuerySystemInformation(SystemExtendedHandleInformation, buffer, size, out int needed);
                if (status == StatusInfoLengthMismatch || status == StatusBufferOverflow || status == StatusBufferTooSmall)
                {
                    int next = Math.Max(needed + 256 * 1024, size * 2);
                    if (next > MaxSnapshotBytes)
                        throw new InvalidOperationException("The system handle table is too large to inspect.");
                    size = next;
                    continue;
                }

                if (status < 0)
                    throw new InvalidOperationException($"Couldn't read the system handle table (NTSTATUS 0x{status:X8}).");

                long count = Marshal.ReadIntPtr(buffer).ToInt64();
                long available = (size - EntriesOffset) / EntrySize;
                if (count > available)
                    count = available;

                for (long i = 0; i < count; i++)
                {
                    IntPtr entryPtr = IntPtr.Add(buffer, checked(EntriesOffset + (int)(i * EntrySize)));
                    HandleEntry entry = Marshal.PtrToStructure<HandleEntry>(entryPtr);
                    long pid = entry.UniqueProcessId.ToInt64();
                    if (pid <= 0 || pid > int.MaxValue || !targets.Contains((int)pid))
                        continue;

                    if (!byProcess.TryGetValue((int)pid, out List<IntPtr>? list))
                        byProcess[(int)pid] = list = new List<IntPtr>();
                    list.Add(entry.HandleValue);
                }

                return byProcess;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static string? QueryUnicodeString(IntPtr handle, int infoClass)
    {
        int size = 1024;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int status = NtQueryObject(handle, infoClass, buffer, size, out int needed);
                if (status == StatusInfoLengthMismatch || status == StatusBufferOverflow || status == StatusBufferTooSmall)
                {
                    size = Math.Max(needed, size * 2);
                    continue;
                }

                if (status < 0)
                    return null;

                // Both OBJECT_NAME_INFORMATION and OBJECT_TYPE_INFORMATION start with a UNICODE_STRING.
                UnicodeString text = Marshal.PtrToStructure<UnicodeString>(buffer);
                if (text.Buffer == IntPtr.Zero || text.Length == 0)
                    return string.Empty;
                return Marshal.PtrToStringUni(text.Buffer, text.Length / 2);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HandleEntry
    {
        public IntPtr Object;
        public IntPtr UniqueProcessId;
        public IntPtr HandleValue;
        public uint GrantedAccess;
        public ushort CreatorBackTraceIndex;
        public ushort ObjectTypeIndex;
        public uint HandleAttributes;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [DllImport("ntdll.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int NtQuerySystemInformation(int systemInformationClass, IntPtr systemInformation, int length, out int returnLength);

    [DllImport("ntdll.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int NtQueryObject(IntPtr handle, int objectInformationClass, IntPtr objectInformation, int length, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(
        SafeProcessHandle sourceProcess, IntPtr sourceHandle, IntPtr targetProcess,
        out IntPtr targetHandle, uint desiredAccess, bool inheritHandle, uint options);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
