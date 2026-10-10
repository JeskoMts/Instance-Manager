using System;
using System.Security;
using Microsoft.Win32;

namespace InstanceManager.Services;

public sealed class StartupService
{
    public const string StartupArgument = "--startup";
    public const string DefaultRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "Instance Manager";

    private readonly string _runKey;
    private readonly string _executablePath;

    public StartupService()
        : this(DefaultRunKey, Environment.ProcessPath ?? string.Empty)
    {
    }

    internal StartupService(string runKey, string executablePath)
    {
        _runKey = runKey;
        _executablePath = executablePath;
    }

    public string Command => $"\"{_executablePath}\" {StartupArgument}";

    public bool IsEnabled => ReadValue() is { Length: > 0 };

    public bool SetEnabled(bool enabled)
    {
        if (enabled && string.IsNullOrWhiteSpace(_executablePath))
            return false;

        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(_runKey, writable: true);
            if (enabled)
                key.SetValue(ValueName, Command, RegistryValueKind.String);
            else if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or System.IO.IOException)
        {
            return false;
        }
    }

    public void RefreshPath()
    {
        string? current = ReadValue();
        if (current is { Length: > 0 } && !string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
            SetEnabled(true);
    }

    private string? ReadValue()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_runKey, writable: false);
            return key?.GetValue(ValueName) as string;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or System.IO.IOException)
        {
            return null;
        }
    }
}
