using System;
using System.Threading;
using InstanceManager.Models;
using InstanceManager.Services;

namespace InstanceManager.Storage;

public sealed class SettingsService : ISettingsService, IDisposable
{
    private readonly JsonFileStore _file;
    private readonly TimeSpan _debounce;
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _dirty;
    private bool _disposed;

    public SettingsService() : this(AppPaths.SettingsFile, TimeSpan.FromMilliseconds(350))
    {
        AppPaths.EnsureDataDirectory();
    }

    public SettingsService(string path, TimeSpan debounce)
    {
        _file = new JsonFileStore(path);
        _debounce = debounce < TimeSpan.Zero ? TimeSpan.Zero : debounce;
        Settings = _file.Load(() => new AppSettings());
        bool changed = Settings.Normalize();
        if (!string.IsNullOrWhiteSpace(Settings.VersionsPathOverride))
        {
            if (!LocalPathPolicy.TryNormalizeFixedLocalDirectory(
                    Settings.VersionsPathOverride,
                    out string normalizedPath))
            {
                Settings.VersionsPathOverride = null;
                changed = true;
            }
            else if (!string.Equals(
                         Settings.VersionsPathOverride,
                         normalizedPath,
                         StringComparison.Ordinal))
            {
                Settings.VersionsPathOverride = normalizedPath;
                changed = true;
            }
        }

        if (changed)
            _file.Save(Settings);
    }

    public AppSettings Settings { get; }

    public void Save()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            CancelTimer();
            _dirty = false;
            _file.Save(Settings);
        }
    }

    public void ScheduleSave()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _dirty = true;
            _timer ??= new Timer(static state => ((SettingsService)state!).FlushFromTimer(), this,
                Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            if (_disposed || !_dirty)
                return;

            CancelTimer();
            _dirty = false;
            _file.Save(Settings);
        }
    }

    private void FlushFromTimer()
    {
        try { Flush(); }
        catch (ObjectDisposedException) { }
    }

    private void CancelTimer() =>
        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SettingsService));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            if (_dirty)
            {
                _dirty = false;
                _file.Save(Settings);
            }

            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
