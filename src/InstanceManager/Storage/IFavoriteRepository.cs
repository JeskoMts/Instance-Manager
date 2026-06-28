using System.Collections.Generic;
using InstanceManager.Models;

namespace InstanceManager.Storage;

public interface IFavoriteRepository
{
    IReadOnlyList<FavoriteGame> All { get; }
    void Add(FavoriteGame favorite);
    void Update(FavoriteGame favorite);
    void Remove(FavoriteGame favorite);

    void Replace(IEnumerable<FavoriteGame> favorites);
}
