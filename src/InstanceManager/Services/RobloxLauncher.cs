using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using InstanceManager.Models;

namespace InstanceManager.Services;

public sealed class RobloxLauncher
{
    public const string PlaceLauncherBase = "https://assetgame.roblox.com/game/PlaceLauncher.ashx";
    private readonly IRobloxExecutableValidator _executableValidator;

    public RobloxLauncher(IRobloxExecutableValidator? executableValidator = null) =>
        _executableValidator = executableValidator ?? new RobloxExecutableValidator();

    public static string BuildPlaceLauncherUrl(ServerTarget target, long browserTrackerId)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.Mode switch
        {
            JoinMode.PublicByLink =>
                $"{PlaceLauncherBase}?request=RequestGame" +
                $"&browserTrackerId={browserTrackerId}" +
                $"&placeId={target.PlaceId}" +
                $"&isPlayTogetherGame=false",

            JoinMode.PrivateByJobId =>
                $"{PlaceLauncherBase}?request=RequestGameJob" +
                $"&browserTrackerId={browserTrackerId}" +
                $"&placeId={target.PlaceId}" +
                $"&gameId={Uri.EscapeDataString(target.JobId ?? string.Empty)}" +
                $"&isPlayTogetherGame=false",

            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
    }

    public static string BuildLaunchUrl(string authTicket, string placeLauncherUrl, long browserTrackerId, long launchTimeMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authTicket);
        ArgumentException.ThrowIfNullOrWhiteSpace(placeLauncherUrl);

        return "roblox-player:1" +
               "+launchmode:play" +
               $"+gameinfo:{authTicket}" +
               $"+launchtime:{launchTimeMs}" +
               $"+placelauncherurl:{Uri.EscapeDataString(placeLauncherUrl)}" +
               $"+browsertrackerid:{browserTrackerId}" +
               "+robloxLocale:en_us" +
               "+gameLocale:en_us" +
               "+channel:" +
               "+LaunchExp:InApp";
    }

    public static string BuildLaunchUrl(string authTicket, ServerTarget target, long browserTrackerId, long launchTimeMs)
    {
        string placeLauncher = BuildPlaceLauncherUrl(target, browserTrackerId);
        return BuildLaunchUrl(authTicket, placeLauncher, browserTrackerId, launchTimeMs);
    }

    public static long GenerateBrowserTrackerId() =>
        RandomNumberGenerator.GetInt32(10_000_000, 1_500_000_000);

    public Process Launch(RobloxVersion version, string launchUrl)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (!_executableValidator.TryValidate(version.FolderPath, version.PlayerExePath, out string error))
            throw new InvalidOperationException(error);

        var psi = new ProcessStartInfo
        {
            FileName = version.PlayerExePath,
            WorkingDirectory = version.FolderPath,
            UseShellExecute = false
        };
        psi.ArgumentList.Add(launchUrl);

        Process proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start the Roblox process.");
        return proc;
    }
}
