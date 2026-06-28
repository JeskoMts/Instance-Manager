using System;
using System.IO;
using System.Text;
using InstanceManager.Models;

namespace InstanceManager.Services;

public sealed class AutoReconnectLog
{
    internal const int MaxLogBytes = 1024 * 1024;
    private const int MaxMessageCharacters = 2048;
    private readonly string _path;
    private readonly object _gate = new();

    public AutoReconnectLog() : this(Path.Combine(AppPaths.DataDirectory, "auto-reconnect.log"))
    {
    }

    public AutoReconnectLog(string path) => _path = path;

    public void Attempt(Account account, AutoReconnectTrigger trigger, int attempt, int max, ServerTarget target)
    {
        string where = target.Mode == JoinMode.PrivateByJobId
            ? $"placeId={target.PlaceId} jobId={target.JobId}"
            : $"placeId={target.PlaceId}";
        Write($"RECONNECT  '{account.DisplayLabel}' (userId {account.UserId})  trigger={trigger}  attempt {attempt}/{max}  -> {where}");
    }

    public void Result(Account account, int attempt, bool success, string? detail = null)
    {
        string status = success ? "started" : "FAILED";
        string suffix = string.IsNullOrEmpty(detail) ? string.Empty : $"  ({detail})";
        Write($"RESULT  '{account.DisplayLabel}' (userId {account.UserId})  attempt {attempt}  {status}{suffix}");
    }

    public void GaveUp(Account account, AutoReconnectTrigger trigger, int max)
        => Write($"GIVEUP  '{account.DisplayLabel}' (userId {account.UserId})  trigger={trigger}  retry limit ({max}) reached");

    public void SkippedCollateral(Account account)
        => Write($"SKIP  '{account.DisplayLabel}' (userId {account.UserId})  drop ignored as collateral of another instance's restart");

    public void Write(string message)
    {
        string line =
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}  {SanitizeField(message)}{Environment.NewLine}";
        try
        {
            lock (_gate)
            {
                string? dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(_path) && new FileInfo(_path).Length >= MaxLogBytes)
                    File.Move(_path, _path + ".1", overwrite: true);

                File.AppendAllText(_path, line, Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal static string SanitizeField(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        int length = Math.Min(value.Length, MaxMessageCharacters);
        var builder = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            char c = value[i];
            builder.Append(char.IsControl(c) ? ' ' : c);
        }

        return builder.ToString();
    }
}
