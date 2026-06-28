using System;
using System.Collections.Generic;

namespace InstanceManager.Services;

internal sealed class LogSessionRegistry
{
    private readonly HashSet<string> _claimed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, Launch> _launches = new();
    private readonly Dictionary<Guid, string> _claimsByLaunch = new();
    private readonly object _gate = new();
    private long _nextOrder;

    public Guid RegisterLaunch(DateTime launchUtc)
    {
        lock (_gate)
        {
            Guid id = Guid.NewGuid();
            _launches[id] = new Launch(launchUtc, _nextOrder++);
            return id;
        }
    }

    public bool IsClaimed(string path)
    {
        lock (_gate)
            return _claimed.Contains(path);
    }

    public bool TryClaim(Guid launchId, string path, DateTime sessionStartUtc)
    {
        lock (_gate)
        {
            if (!_launches.TryGetValue(launchId, out Launch launch)
                || _claimsByLaunch.ContainsKey(launchId))
            {
                return false;
            }

            long bestDistance = Math.Abs((sessionStartUtc - launch.StartUtc).Ticks);
            long bestOrder = launch.Order;
            Guid bestId = launchId;

            foreach ((Guid otherId, Launch other) in _launches)
            {
                if (_claimsByLaunch.ContainsKey(otherId))
                    continue;

                long distance = Math.Abs((sessionStartUtc - other.StartUtc).Ticks);
                if (distance < bestDistance || (distance == bestDistance && other.Order < bestOrder))
                {
                    bestDistance = distance;
                    bestOrder = other.Order;
                    bestId = otherId;
                }
            }

            if (bestId != launchId || !_claimed.Add(path))
                return false;

            _claimsByLaunch[launchId] = path;
            return true;
        }
    }

    public void UnregisterLaunch(Guid launchId)
    {
        lock (_gate)
        {
            if (_claimsByLaunch.Remove(launchId, out string? path))
                _claimed.Remove(path);
            _launches.Remove(launchId);
        }
    }

    private readonly record struct Launch(DateTime StartUtc, long Order);
}
