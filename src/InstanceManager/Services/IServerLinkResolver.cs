using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;

namespace InstanceManager.Services;

public interface IServerLinkResolver
{
    Task<ServerTargetResolution> ResolveAsync(string input, CancellationToken cancellationToken = default);

    Task<ServerTargetResolution> ResolveAsync(
        string input, IReadOnlyList<Account> accounts, CancellationToken cancellationToken = default) =>
        ResolveAsync(input, cancellationToken);
}
