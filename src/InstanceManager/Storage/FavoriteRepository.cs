using System.Collections.Generic;
using System.Linq;
using InstanceManager.Models;
using InstanceManager.Services;

namespace InstanceManager.Storage;

public sealed class FavoriteRepository : IFavoriteRepository
{
    private readonly JsonFileStore _file = new(AppPaths.FavoritesFile);
    private readonly List<FavoriteGame> _items;

    public FavoriteRepository()
    {
        AppPaths.EnsureDataDirectory();
        _items = _file.Load(() => new List<FavoriteGame>());
    }

    public IReadOnlyList<FavoriteGame> All => _items;

    public void Add(FavoriteGame favorite)
    {
        _items.Add(favorite);
        _file.Save(_items);
    }

    public void Update(FavoriteGame favorite)
    {
        int index = _items.FindIndex(f => f.Id == favorite.Id);
        if (index < 0)
            return;

        _items[index] = favorite;
        _file.Save(_items);
    }

    public void Remove(FavoriteGame favorite)
    {
        _items.RemoveAll(f => f.Id == favorite.Id);
        _file.Save(_items);
    }

    public void Replace(IEnumerable<FavoriteGame> favorites)
    {
        _items.Clear();
        _items.AddRange(favorites);
        _file.Save(_items);
    }
}
