using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;

namespace InstanceManager.Services;

public sealed record PublicServer(string JobId, int Playing, int MaxPlayers);

public interface IRobloxGamesService
{
    Task<IReadOnlyList<GameInfo>> GetPopularAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameInfo>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<byte[]?> GetThumbnailAsync(string? imageUrl, CancellationToken cancellationToken = default);

    Task<string?> GetGameNameAsync(long placeId, CancellationToken cancellationToken = default);

    Task<long?> GetRootPlaceIdAsync(long universeId, CancellationToken cancellationToken = default) =>
        Task.FromResult<long?>(null);

    Task<IReadOnlyList<PublicServer>> GetPublicServersAsync(long placeId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PublicServer>>(System.Array.Empty<PublicServer>());
}
