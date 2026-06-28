using InstanceManager.Models;

namespace InstanceManager.Services;

public sealed record ServerTargetResolution(ServerTarget? Target, string Error)
{
    public bool IsSuccess => Target != null;

    public static ServerTargetResolution Success(ServerTarget target) => new(target, string.Empty);
    public static ServerTargetResolution Failure(string error) => new(null, error);
}
