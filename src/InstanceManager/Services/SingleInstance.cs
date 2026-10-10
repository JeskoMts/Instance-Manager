using System;
using System.IO;
using System.Threading;

namespace InstanceManager.Services;

public sealed class SingleInstance : IDisposable
{
    public const string DefaultName = "JeskoMts.InstanceManager";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _listener;
    private bool _released;

    private SingleInstance(Mutex mutex, EventWaitHandle activation)
    {
        _mutex = mutex;
        _activation = activation;
    }

    public static SingleInstance? TryAcquire(string name, TimeSpan wait)
    {
        var mutex = new Mutex(false, MutexName(name));
        bool owned;
        try
        {
            owned = mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            owned = true;
        }

        if (!owned)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstance(mutex, OpenActivation(name));
    }

    public static bool SignalExisting(string name)
    {
        try
        {
            using EventWaitHandle activation = OpenActivation(name);
            return activation.Set();
        }
        catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    public void ListenForActivation(Action onActivate)
    {
        ArgumentNullException.ThrowIfNull(onActivate);
        _listener?.Unregister(null);
        _listener = ThreadPool.RegisterWaitForSingleObject(
            _activation, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Release()
    {
        if (_released)
            return;
        _released = true;
        _listener?.Unregister(null);
        _listener = null;
        try { _mutex.ReleaseMutex(); }
        catch (ApplicationException) { }
    }

    public void Dispose()
    {
        Release();
        _mutex.Dispose();
        _activation.Dispose();
    }

    private static EventWaitHandle OpenActivation(string name) =>
        new(false, EventResetMode.AutoReset, $"Local\\{name}.Activate");

    private static string MutexName(string name) => $"Local\\{name}.Instance";
}
