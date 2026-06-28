using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace InstanceManager.Services;

public sealed class RobloxLogWatcher : IDisposable
{
    private const int PollIntervalMs = 1000;
    private static readonly TimeSpan SelectionTolerance = TimeSpan.FromSeconds(5);

    private readonly string _logsDirectory;
    private readonly DateTime _launchUtc;
    private readonly LogSessionRegistry _registry;
    private readonly Guid _launchId;
    private readonly Timer _timer;
    private readonly object _gate = new();

    private string? _logPath;
    private long _offset;
    private bool _disposed;

    public event Action<RobloxSessionSignal>? Detected;

    public RobloxLogWatcher(DateTime launchUtc)
        : this(DefaultLogsDirectory, launchUtc, new LogSessionRegistry())
    {
    }

    public RobloxLogWatcher(string logsDirectory, DateTime launchUtc)
        : this(logsDirectory, launchUtc, new LogSessionRegistry())
    {
    }

    internal RobloxLogWatcher(DateTime launchUtc, LogSessionRegistry registry)
        : this(DefaultLogsDirectory, launchUtc, registry)
    {
    }

    internal RobloxLogWatcher(string logsDirectory, DateTime launchUtc, LogSessionRegistry registry)
    {
        _logsDirectory = logsDirectory;
        _launchUtc = launchUtc;
        _registry = registry;
        _launchId = _registry.RegisterLaunch(launchUtc);
        _timer = new Timer(_ => Poll(), null, PollIntervalMs, PollIntervalMs);
    }

    public static string DefaultLogsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox", "logs");

    private void Poll()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            try
            {
                _logPath ??= FindSessionLog();
                if (_logPath == null)
                    return;

                ReadNewLines(_logPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
            }
        }
    }

    private string? FindSessionLog()
    {
        if (!Directory.Exists(_logsDirectory))
            return null;

        DateTime threshold = _launchUtc - SelectionTolerance;
        string? best = null;
        long bestDistance = long.MaxValue;
        DateTime bestTime = DateTime.MaxValue;

        foreach (string file in Directory.EnumerateFiles(_logsDirectory, "*.log"))
        {
            DateTime sessionStart = GetSessionStartUtc(file);
            if (sessionStart < threshold)
                continue;

            if (_registry.IsClaimed(file))
                continue;

            long distance = Math.Abs((sessionStart - _launchUtc).Ticks);
            if (distance < bestDistance || (distance == bestDistance && sessionStart < bestTime))
            {
                bestDistance = distance;
                bestTime = sessionStart;
                best = file;
            }
        }

        if (best != null && !_registry.TryClaim(_launchId, best, bestTime))
            return null;

        return best;
    }

    private static DateTime GetSessionStartUtc(string path)
    {
        DateTime createdUtc = File.GetCreationTimeUtc(path);
        string name = Path.GetFileName(path);
        int marker = name.IndexOf("_Player_", StringComparison.OrdinalIgnoreCase);
        if (marker >= 16)
        {
            string stamp = name.Substring(marker - 16, 16);
            if (DateTime.TryParseExact(
                    stamp,
                    "yyyyMMdd'T'HHmmss'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime parsed))
            {
                if (createdUtc >= parsed && createdUtc < parsed.AddSeconds(1))
                    return createdUtc;
                return parsed;
            }
        }

        return createdUtc;
    }

    private void ReadNewLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length < _offset)
            _offset = 0;

        stream.Seek(_offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            RobloxSessionSignal? signal = RobloxLogClassifier.Classify(line);
            if (signal.HasValue)
                Detected?.Invoke(signal.Value);
        }

        _offset = stream.Position;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        _timer.Dispose();
        _registry.UnregisterLaunch(_launchId);
        Detected = null;
    }
}
