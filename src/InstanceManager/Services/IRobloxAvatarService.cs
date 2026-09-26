using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Services;

public interface IRobloxAvatarService
{
    Task<byte[]?> GetAvatarAsync(long userId, CancellationToken cancellationToken = default);

    Task<byte[]?> GetCachedAvatarAsync(long userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<byte[]?>(null);
}
