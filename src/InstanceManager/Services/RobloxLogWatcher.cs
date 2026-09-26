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
    private FileStream? _stream;
    private StreamReader? _reader;
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

                ReadNewLines();
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

        foreach (FileInfo info in new DirectoryInfo(_logsDirectory).EnumerateFiles("*.log"))
        {
            string file = info.FullName;
            DateTime sessionStart = GetSessionStartUtc(file, info.CreationTimeUtc);
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

    private static DateTime GetSessionStartUtc(string path, DateTime createdUtc)
    {
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

    private void ReadNewLines()
    {
        if (_reader == null)
        {
            _stream = new FileStream(_logPath!, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, bufferSize: 1, FileOptions.SequentialScan);
            _reader = new StreamReader(_stream, bufferSize: 16 * 1024);
        }
        else if (_stream!.Length < _stream.Position)
        {
            _stream.Position = 0;
            _reader.DiscardBufferedData();
        }

        string? line;
        while ((line = _reader.ReadLine()) != null)
        {
            RobloxSessionSignal? signal = RobloxLogClassifier.Classify(line);
            if (signal.HasValue)
                Detected?.Invoke(signal.Value);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _reader?.Dispose();
            _reader = null;
            _stream = null;
        }

        _timer.Dispose();
        _registry.UnregisterLaunch(_launchId);
        Detected = null;
    }
}
