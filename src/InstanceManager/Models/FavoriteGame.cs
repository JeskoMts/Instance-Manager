using System;
using System.Text.Json.Serialization;

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

    public string? PrivateServerLinkCode { get; set; }

    public int SortOrder { get; set; }

    [JsonIgnore]
    public string? ServerLink =>
        !string.IsNullOrWhiteSpace(DefaultJobId)
            ? $"https://www.roblox.com/games/start?placeId={PlaceId}&gameInstanceId={DefaultJobId}"
            : !string.IsNullOrWhiteSpace(PrivateServerLinkCode)
                ? $"https://www.roblox.com/games/{PlaceId}?privateServerLinkCode={PrivateServerLinkCode}"
                : null;
}
