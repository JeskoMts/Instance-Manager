using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InstanceManager.ViewModels;

public partial class GraphicsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly RobloxGraphicsService _graphics;
    private readonly Func<IEnumerable<string>> _versionFolders;
    private readonly DispatcherTimer? _applyTimer;
    private bool _loading;

    public GraphicsViewModel(ISettingsService settings, RobloxGraphicsService graphics, Func<IEnumerable<string>> versionFolders)
    {
        _settings = settings;
        _graphics = graphics;
        _versionFolders = versionFolders;

        if (System.Windows.Application.Current != null)
        {
            _applyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _applyTimer.Tick += (_, _) =>
            {
                _applyTimer.Stop();
                ApplyNow();
            };
        }

        _loading = true;
        AppSettings s = settings.Settings;
        fpsCap = s.FpsCap;
        applyFastFlags = s.ApplyFastFlags;
        graphicsApi = s.GraphicsApi;
        graphicsQuality = s.GraphicsQuality;
        antiAliasing = s.AntiAliasing;
        textureQuality = s.TextureQuality;
        ignoreDisplayScaling = s.IgnoreDisplayScaling;
        hideGrass = s.HideGrass;
        graySky = s.GraySky;
        freezeLighting = s.FreezeLighting;
        exclusiveFullscreen = s.ExclusiveFullscreen;
        customFastFlags = s.CustomFastFlags;
        _loading = false;
        RefreshSummary();
    }

    public IReadOnlyList<Choice<int>> FpsCapChoices { get; } = AppSettings.FpsCapChoices
        .Select(value => new Choice<int>(value, value == 0 ? "Roblox default" : $"{value} FPS"))
        .ToList();

    public IReadOnlyList<Choice<GraphicsApi>> GraphicsApiChoices { get; } = new[]
    {
        new Choice<GraphicsApi>(GraphicsApi.Automatic, "Automatic"),
        new Choice<GraphicsApi>(GraphicsApi.Direct3D11, "Direct3D 11"),
        new Choice<GraphicsApi>(GraphicsApi.Vulkan, "Vulkan"),
        new Choice<GraphicsApi>(GraphicsApi.OpenGL, "OpenGL")
    };

    public IReadOnlyList<Choice<int>> QualityChoices { get; } = Enumerable.Range(0, AppSettings.MaxGraphicsQuality + 1)
        .Select(step => new Choice<int>(step, step switch
        {
            0 => "Automatic",
            1 => "1 (lowest)",
            AppSettings.MaxGraphicsQuality => $"{step} (highest)",
            _ => step.ToString()
        }))
        .ToList();

    public IReadOnlyList<Choice<int>> AntiAliasingChoices { get; } = new[]
    {
        new Choice<int>(-1, "Automatic"),
        new Choice<int>(0, "Off"),
        new Choice<int>(1, "1x"),
        new Choice<int>(2, "2x"),
        new Choice<int>(4, "4x"),
        new Choice<int>(8, "8x")
    };

    public IReadOnlyList<Choice<int>> TextureChoices { get; } = new[]
    {
        new Choice<int>(-1, "Automatic"),
        new Choice<int>(0, "Low"),
        new Choice<int>(1, "Medium"),
        new Choice<int>(2, "High"),
        new Choice<int>(3, "Highest")
    };

    [ObservableProperty] private int fpsCap;
    [ObservableProperty] private bool applyFastFlags;
    [ObservableProperty] private GraphicsApi graphicsApi;
    [ObservableProperty] private int graphicsQuality;
    [ObservableProperty] private int antiAliasing;
    [ObservableProperty] private int textureQuality;
    [ObservableProperty] private bool ignoreDisplayScaling;
    [ObservableProperty] private bool hideGrass;
    [ObservableProperty] private bool graySky;
    [ObservableProperty] private bool freezeLighting;
    [ObservableProperty] private bool exclusiveFullscreen;
    [ObservableProperty] private string customFastFlags;

    [ObservableProperty] private string flagSummary = string.Empty;
    [ObservableProperty] private string? customFlagsMessage;
    [ObservableProperty] private bool customFlagsHasError;
    [ObservableProperty] private string robloxFpsText = string.Empty;

    partial void OnFpsCapChanged(int value) => Store(s => s.FpsCap = value, applyFps: true);
    partial void OnApplyFastFlagsChanged(bool value) => Store(s => s.ApplyFastFlags = value);
    partial void OnGraphicsApiChanged(GraphicsApi value) => Store(s => s.GraphicsApi = value);
    partial void OnGraphicsQualityChanged(int value) => Store(s => s.GraphicsQuality = value);
    partial void OnAntiAliasingChanged(int value) => Store(s => s.AntiAliasing = value);
    partial void OnTextureQualityChanged(int value) => Store(s => s.TextureQuality = value);
    partial void OnIgnoreDisplayScalingChanged(bool value) => Store(s => s.IgnoreDisplayScaling = value);
    partial void OnHideGrassChanged(bool value) => Store(s => s.HideGrass = value);
    partial void OnGraySkyChanged(bool value) => Store(s => s.GraySky = value);
    partial void OnFreezeLightingChanged(bool value) => Store(s => s.FreezeLighting = value);
    partial void OnExclusiveFullscreenChanged(bool value) => Store(s => s.ExclusiveFullscreen = value);
    partial void OnCustomFastFlagsChanged(string value) => Store(s => s.CustomFastFlags = value ?? string.Empty);

    private void Store(Action<AppSettings> update, bool applyFps = false)
    {
        if (_loading)
            return;

        update(_settings.Settings);
        _settings.Save();
        if (applyFps)
            _graphics.ApplyFramerateCap();
        RefreshSummary();

        if (_applyTimer != null)
        {
            _applyTimer.Stop();
            _applyTimer.Start();
        }
        else
        {
            ApplyNow();
        }
    }

    public void ApplyNow()
    {
        _graphics.ApplyToVersions(_versionFolders());
        RefreshSummary();
    }

    public void RefreshSummary()
    {
        FastFlagPlan plan = _graphics.BuildPlan();
        int count = plan.Flags.Count;
        string flags = count switch
        {
            0 => "No flags set",
            1 => "1 flag set",
            _ => $"{count} flags set"
        };
        FlagSummary = count == 0 ? flags : flags + (ApplyFastFlags ? ", applied" : ", not applied");

        CustomFlagsHasError = plan.CustomError != null;
        CustomFlagsMessage = plan.CustomError
            ?? (plan.Skipped.Count > 0
                ? "Skipped because Roblox ignores them: " + string.Join(", ", plan.Skipped)
                : null);

        int? current = _graphics.ReadFramerateCap();
        RobloxFpsText = current switch
        {
            null => "Roblox hasn't saved a frame rate setting yet.",
            <= 0 => "Roblox uses its own default right now.",
            _ => $"Roblox is set to {current} FPS right now."
        };
    }
}
