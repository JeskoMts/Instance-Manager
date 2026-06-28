using System;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Services;

public sealed class MultiInstanceManager : IDisposable
{
    internal const string CurrentMutexName = "ROBLOX_singletonEvent";
    internal const string LegacyMutexName = "ROBLOX_singletonMutex";

    private readonly object _gate = new();
    private readonly string[] _mutexNames;
    private HoldSession? _session;
    private bool _disposed;

    public MultiInstanceManager() : this(CurrentMutexName, LegacyMutexName)
    {
    }

    internal MultiInstanceManager(string currentMutexName, string legacyMutexName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentMutexName);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyMutexName);
        if (string.Equals(currentMutexName, legacyMutexName, StringComparison.Ordinal))
            throw new ArgumentException("The current and legacy Roblox mutex names must be different.");

        _mutexNames = [currentMutexName, legacyMutexName];
    }

    public bool IsHeld
    {
        get
        {
            lock (_gate)
                return _session?.IsReady == true;
        }
    }

    public void EnsureHeld()
    {
        HoldSession session;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            session = _session ??= new HoldSession(_mutexNames);
        }

        try
        {
            session.WaitUntilReady();
        }
        catch
        {
            lock (_gate)
            {
                if (ReferenceEquals(_session, session))
                    _session = null;
            }

            session.Stop();
            throw;
        }
    }

    public void Release()
    {
        HoldSession? session;
        lock (_gate)
        {
            if (_disposed)
                return;

            session = _session;
            _session = null;
        }

        session?.Stop();
    }

    public void Apply(bool enabled)
    {
        if (enabled)
            EnsureHeld();
        else
            Release();
    }

    public bool TryApply(bool enabled)
    {
        try
        {
            Apply(enabled);
            return true;
        }
        catch (InvalidOperationException ex) when (ex is not ObjectDisposedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        HoldSession? session;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            session = _session;
            _session = null;
        }

        session?.Stop();
    }

    private sealed class HoldSession
    {
        private readonly ManualResetEvent _stop = new(false);
        private readonly MutexSlot[] _slots;
        private int _ready;
        private int _stopped;

        public HoldSession(string[] mutexNames)
        {
            _slots = new MutexSlot[mutexNames.Length];
            for (int i = 0; i < mutexNames.Length; i++)
                _slots[i] = new MutexSlot(mutexNames[i], _stop);

            foreach (MutexSlot slot in _slots)
                slot.Start();
        }

        public bool IsReady => Volatile.Read(ref _ready) != 0;

        public void WaitUntilReady()
        {
            foreach (MutexSlot slot in _slots)
                slot.WaitUntilReady();

            Volatile.Write(ref _ready, 1);
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;

            _stop.Set();
            foreach (MutexSlot slot in _slots)
                slot.Join();
            _stop.Dispose();
        }
    }

    private sealed class MutexSlot
    {
        private readonly string _name;
        private readonly WaitHandle _stop;
        private readonly TaskCompletionSource _initialized =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Thread _thread;

        public MutexSlot(string name, WaitHandle stop)
        {
            _name = name;
            _stop = stop;
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = $"InstanceManager mutex owner: {name}"
            };
        }

        public void Start() => _thread.Start();

        public void WaitUntilReady() => _initialized.Task.GetAwaiter().GetResult();

        public void Join()
        {
            if (_thread.IsAlive)
                _thread.Join();
        }

        private void Run()
        {
            Mutex? mutex = null;
            bool ownsMutex = false;

            try
            {
                try
                {
                    mutex = new Mutex(false, _name);
                }
                catch (WaitHandleCannotBeOpenedException ex)
                {
                    throw new InvalidOperationException(
                        $"Cannot enable Roblox multi-instance: '{_name}' is occupied by a named object that is not a mutex. Close older InstanceManager or Roblox helper processes and try again.",
                        ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Cannot open the Roblox multi-instance mutex '{_name}'.",
                        ex);
                }

                ownsMutex = TryAcquire(mutex);
                _initialized.TrySetResult();

                if (!ownsMutex)
                {
                    try
                    {
                        ownsMutex = WaitHandle.WaitAny([_stop, mutex]) == 1;
                    }
                    catch (AbandonedMutexException ex) when (ex.MutexIndex == 1)
                    {
                        ownsMutex = true;
                    }
                }

                if (ownsMutex)
                    _stop.WaitOne();
            }
            catch (Exception ex)
            {
                _initialized.TrySetException(ex);
            }
            finally
            {
                if (ownsMutex && mutex != null)
                {
                    try { mutex.ReleaseMutex(); }
                    catch (ApplicationException) { }
                }

                mutex?.Dispose();
            }
        }

        private static bool TryAcquire(Mutex mutex)
        {
            try
            {
                return mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
        }
    }
}
