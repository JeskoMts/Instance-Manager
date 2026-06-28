using System;
using InstanceManager.Models;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public class RobloxLauncherTests
{
    [Fact]
    public void BuildPlaceLauncherUrl_Public_UsesRequestGame()
    {
        var target = ServerTarget.Public(126884695634066);
        string url = RobloxLauncher.BuildPlaceLauncherUrl(target, 12345);

        Assert.Contains("request=RequestGame", url);
        Assert.DoesNotContain("RequestGameJob", url);
        Assert.Contains("placeId=126884695634066", url);
        Assert.Contains("browserTrackerId=12345", url);
        Assert.StartsWith(RobloxLauncher.PlaceLauncherBase, url);
    }

    [Fact]
    public void BuildPlaceLauncherUrl_JobId_UsesRequestGameJobWithGameId()
    {
        var target = ServerTarget.ByJob(606849621, "ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab");
        string url = RobloxLauncher.BuildPlaceLauncherUrl(target, 999);

        Assert.Contains("request=RequestGameJob", url);
        Assert.Contains("placeId=606849621", url);
        Assert.Contains("gameId=ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab", url);
    }

    [Fact]
    public void BuildLaunchUrl_ContainsTicketAndEncodedPlaceLauncher()
    {
        var target = ServerTarget.Public(920587237);
        string launchUrl = RobloxLauncher.BuildLaunchUrl("TICKET123", target, 555, 1_700_000_000_000);

        Assert.StartsWith("roblox-player:1", launchUrl);
        Assert.Contains("+launchmode:play", launchUrl);
        Assert.Contains("gameinfo:TICKET123", launchUrl);
        Assert.Contains("launchtime:1700000000000", launchUrl);
        Assert.Contains("browsertrackerid:555", launchUrl);
        Assert.Contains("placelauncherurl:https%3A%2F%2F", launchUrl);
        Assert.DoesNotContain("placelauncherurl:https://", launchUrl);
    }

    [Fact]
    public void BuildLaunchUrl_EndsWithLaunchExpInApp()
    {
        var target = ServerTarget.Public(920587237);
        string launchUrl = RobloxLauncher.BuildLaunchUrl("TICKET123", target, 555, 1_700_000_000_000);

        Assert.EndsWith("+LaunchExp:InApp", launchUrl);
    }

    [Fact]
    public void BuildLaunchUrl_RoundTripsPlaceLauncher()
    {
        var target = ServerTarget.ByJob(1, "ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab");
        string expectedPlaceLauncher = RobloxLauncher.BuildPlaceLauncherUrl(target, 42);
        string launchUrl = RobloxLauncher.BuildLaunchUrl("T", target, 42, 1);

        string encoded = Uri.EscapeDataString(expectedPlaceLauncher);
        Assert.Contains("placelauncherurl:" + encoded, launchUrl);
    }
}
