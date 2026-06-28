using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;

namespace InstanceManager.Services;

public interface IRobloxGamesService
{
    Task<IReadOnlyList<GameInfo>> GetPopularAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameInfo>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<byte[]?> GetThumbnailAsync(string? imageUrl, CancellationToken cancellationToken = default);
}
