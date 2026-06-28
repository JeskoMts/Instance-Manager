namespace InstanceManager.Models;

public enum JoinMode
{
    PublicByLink,

    PrivateByJobId
}

public sealed class ServerTarget
{
    public required JoinMode Mode { get; init; }

    public required long PlaceId { get; init; }

    public string? JobId { get; init; }

    public static ServerTarget Public(long placeId) =>
        new() { Mode = JoinMode.PublicByLink, PlaceId = placeId };

    public static ServerTarget ByJob(long placeId, string jobId) =>
        new() { Mode = JoinMode.PrivateByJobId, PlaceId = placeId, JobId = jobId };
}
