namespace InstanceManager.Models;

public sealed record GameInfo(
    long UniverseId,
    long PlaceId,
    string Name,
    string CreatorName,
    long PlayerCount,
    string? ThumbnailUrl);
