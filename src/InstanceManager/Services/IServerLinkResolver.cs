using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Services;

public interface IServerLinkResolver
{
    Task<ServerTargetResolution> ResolveAsync(string input, CancellationToken cancellationToken = default);
}
