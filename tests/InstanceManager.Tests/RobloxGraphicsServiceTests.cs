using System;
using System.IO;
using System.Linq;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using Xunit;

namespace InstanceManager.Tests;

public sealed class RobloxGraphicsServiceTests : IDisposable
{
    private const string BasicSettings =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><roblox version=\"4\"><Item class=\"UserGameSettings\"><Properties>" +
        "<int name=\"FramerateCap\">-1</int><int name=\"GraphicsQualityLevel\">5</int></Properties></Item></roblox>";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "im-graphics-" + Guid.NewGuid().ToString("N"));
    private readonly FakeSettingsService _settings = new();
    private readonly RobloxGraphicsService _service;

    public RobloxGraphicsServiceTests()
    {
        Directory.CreateDirectory(_root);
        _service = new RobloxGraphicsService(_settings, _root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void BuildPlan_DefaultSettingsChangeNothing()
    {
        FastFlagPlan plan = _service.BuildPlan();

        Assert.Empty(plan.Flags);
        Assert.Empty(plan.Skipped);
        Assert.Null(plan.CustomError);
    }

    [Fact]
    public void BuildPlan_TurnsEveryOptionIntoAllowlistedFlags()
    {
        AppSettings s = _settings.Settings;
        s.GraphicsApi = GraphicsApi.Vulkan;
        s.GraphicsQuality = 10;
        s.AntiAliasing = 4;
        s.TextureQuality = 0;
        s.IgnoreDisplayScaling = true;
        s.HideGrass = true;
        s.GraySky = true;
        s.FreezeLighting = true;
        s.ExclusiveFullscreen = true;

        FastFlagPlan plan = _service.BuildPlan();

        Assert.Equal("True", plan.Flags["FFlagDebugGraphicsPreferVulkan"]);
        Assert.Equal("21", plan.Flags["DFIntDebugFRMQualityLevelOverride"]);
        Assert.Equal("4", plan.Flags["FIntDebugForceMSAASamples"]);
        Assert.Equal("True", plan.Flags["DFFlagTextureQualityOverrideEnabled"]);
        Assert.Equal("0", plan.Flags["DFIntTextureQualityOverride"]);
        Assert.Equal("True", plan.Flags["DFFlagDisableDPIScale"]);
        Assert.Equal("0", plan.Flags["FIntFRMMaxGrassDistance"]);
        Assert.Equal("True", plan.Flags["FFlagDebugSkyGray"]);
        Assert.Equal("True", plan.Flags["DFFlagDebugPauseVoxelizer"]);
        Assert.Equal("False", plan.Flags["FFlagHandleAltEnterFullscreenManually"]);
        Assert.All(plan.Flags.Keys, key => Assert.Contains(key, RobloxGraphicsService.Allowlist));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 10)]
    [InlineData(10, 21)]
    [InlineData(99, 21)]
    public void QualityLevel_SpreadsTenStepsOverRobloxLevels(int step, int level)
    {
        Assert.Equal(level, RobloxGraphicsService.QualityLevel(step));
    }

    [Fact]
    public void CustomFlags_OnlyAllowlistedNamesAreUsed()
    {
        _settings.Settings.CustomFastFlags = "{\"FFlagDebugSkyGray\": true, \"DFIntTextureQualityOverride\": 2, \"FFlagSomethingElse\": \"True\", \"FIntDebugForceMSAASamples\": {}}";

        FastFlagPlan plan = _service.BuildPlan();

        Assert.Equal("True", plan.Flags["FFlagDebugSkyGray"]);
        Assert.Equal("2", plan.Flags["DFIntTextureQualityOverride"]);
        Assert.Equal(new[] { "FFlagSomethingElse", "FIntDebugForceMSAASamples" }, plan.Skipped);
        Assert.Null(plan.CustomError);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[1, 2]")]
    public void CustomFlags_InvalidInputReportsAnError(string json)
    {
        _settings.Settings.CustomFastFlags = json;

        FastFlagPlan plan = _service.BuildPlan();

        Assert.NotNull(plan.CustomError);
        Assert.Empty(plan.Flags);
    }

    [Fact]
    public void ApplyToVersions_WritesOnceAndRemovesOnlyItsOwnFileWhenTurnedOff()
    {
        string version = Path.Combine(_root, "Versions", "version-abc");
        string file = Path.Combine(version, "ClientSettings", RobloxGraphicsService.SettingsFileName);
        _settings.Settings.ApplyFastFlags = true;
        _settings.Settings.GraySky = true;

        Assert.Equal(1, _service.ApplyToVersions(new[] { version }));
        Assert.Contains("FFlagDebugSkyGray", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.NotNull(_settings.Settings.AppliedFastFlagsHash);
        Assert.Equal(0, _service.ApplyToVersions(new[] { version }));

        _settings.Settings.ApplyFastFlags = false;
        _service.ApplyToVersions(new[] { version });

        Assert.False(File.Exists(file));
        Assert.Null(_settings.Settings.AppliedFastFlagsHash);
    }

    [Fact]
    public void ApplyToVersions_KeepsAFileTheUserChanged()
    {
        string version = Path.Combine(_root, "Versions", "version-def");
        string file = Path.Combine(version, "ClientSettings", RobloxGraphicsService.SettingsFileName);
        _settings.Settings.ApplyFastFlags = true;
        _settings.Settings.GraySky = true;
        _service.ApplyToVersions(new[] { version });
        File.WriteAllText(file, "{\"FFlagMine\": \"True\"}");

        _settings.Settings.ApplyFastFlags = false;
        _service.ApplyToVersions(new[] { version });

        Assert.Equal("{\"FFlagMine\": \"True\"}", File.ReadAllText(file));
    }

    [Fact]
    public void FramerateCap_IsReadAndWrittenInRobloxSettings()
    {
        File.WriteAllText(_service.BasicSettingsPath, BasicSettings);

        Assert.Equal(-1, _service.ReadFramerateCap());
        Assert.False(_service.ApplyFramerateCap());

        _settings.Settings.FpsCap = 144;
        Assert.True(_service.ApplyFramerateCap());

        Assert.Equal(144, _service.ReadFramerateCap());
        string xml = File.ReadAllText(_service.BasicSettingsPath);
        Assert.Contains("<int name=\"GraphicsQualityLevel\">5</int>", xml, StringComparison.Ordinal);
        Assert.Equal(1, xml.Split("FramerateCap").Length - 1);
    }

    [Fact]
    public void FramerateCap_MissingOrBrokenFileIsLeftAlone()
    {
        _settings.Settings.FpsCap = 60;

        Assert.Null(_service.ReadFramerateCap());
        Assert.False(_service.ApplyFramerateCap());
        Assert.False(File.Exists(_service.BasicSettingsPath));

        File.WriteAllText(_service.BasicSettingsPath, "<roblox><broken");
        Assert.Null(_service.ReadFramerateCap());
        Assert.False(_service.ApplyFramerateCap());
    }

    [Fact]
    public void NormalizeGraphics_ResetsUnknownChoices()
    {
        var settings = new AppSettings { FpsCap = 59, AntiAliasing = 3, TextureQuality = 9, GraphicsQuality = 42 };

        Assert.True(settings.Normalize());

        Assert.Equal(0, settings.FpsCap);
        Assert.Equal(-1, settings.AntiAliasing);
        Assert.Equal(-1, settings.TextureQuality);
        Assert.Equal(0, settings.GraphicsQuality);
        Assert.Contains(144, AppSettings.FpsCapChoices);
        Assert.True(AppSettings.FpsCapChoices.SequenceEqual(AppSettings.FpsCapChoices.OrderBy(v => v)));
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public void Save() { }
    }
}
