using System;

namespace InstanceManager.Models;

public sealed class FavoriteGame : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private bool _isPrimary;

    public bool IsPrimary
    {
        get => _isPrimary;
        set => SetProperty(ref _isPrimary, value);
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public long PlaceId { get; set; }

    public string? DefaultJobId { get; set; }

    public int SortOrder { get; set; }
}
