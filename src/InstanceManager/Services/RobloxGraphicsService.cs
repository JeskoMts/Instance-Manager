using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using InstanceManager.Models;
using InstanceManager.Storage;

namespace InstanceManager.Services;

public sealed record FastFlagPlan(IReadOnlyDictionary<string, string> Flags, IReadOnlyList<string> Skipped, string? CustomError);

public sealed class RobloxGraphicsService
{
    public const string SettingsFileName = "ClientAppSettings.json";
    public const string BasicSettingsFileName = "GlobalBasicSettings_13.xml";
    private const int MaxCustomFlagsCharacters = 64 * 1024;

    public static readonly IReadOnlyList<string> Allowlist = new[]
    {
        "DFIntCSGLevelOfDetailSwitchingDistance",
        "DFIntCSGLevelOfDetailSwitchingDistanceL12",
        "DFIntCSGLevelOfDetailSwitchingDistanceL23",
        "DFIntCSGLevelOfDetailSwitchingDistanceL34",
        "FFlagHandleAltEnterFullscreenManually",
        "DFFlagTextureQualityOverrideEnabled",
        "DFIntTextureQualityOverride",
        "FIntDebugForceMSAASamples",
        "DFFlagDisableDPIScale",
        "FFlagDebugGraphicsPreferD3D11",
        "FFlagDebugGraphicsPreferVulkan",
        "FFlagDebugGraphicsPreferOpenGL",
        "FFlagDebugSkyGray",
        "DFFlagDebugPauseVoxelizer",
        "DFIntDebugFRMQualityLevelOverride",
        "FIntFRMMaxGrassDistance",
        "FIntFRMMinGrassDistance",
        "FIntGrassMovementReducedMotionFactor"
    };

    private static readonly HashSet<string> Allowed = new(Allowlist, StringComparer.Ordinal);

    private readonly ISettingsService _settings;
    private readonly string _robloxDirectory;

    public RobloxGraphicsService(ISettingsService settings)
        : this(settings, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox"))
    {
    }

    internal RobloxGraphicsService(ISettingsService settings, string robloxDirectory)
    {
        _settings = settings;
        _robloxDirectory = robloxDirectory;
    }

    public string BasicSettingsPath => Path.Combine(_robloxDirectory, BasicSettingsFileName);

    public FastFlagPlan BuildPlan()
    {
        AppSettings s = _settings.Settings;
        var flags = new SortedDictionary<string, string>(StringComparer.Ordinal);

        switch (s.GraphicsApi)
        {
            case GraphicsApi.Direct3D11: flags["FFlagDebugGraphicsPreferD3D11"] = "True"; break;
            case GraphicsApi.Vulkan: flags["FFlagDebugGraphicsPreferVulkan"] = "True"; break;
            case GraphicsApi.OpenGL: flags["FFlagDebugGraphicsPreferOpenGL"] = "True"; break;
        }

        if (s.GraphicsQuality is > 0 and <= AppSettings.MaxGraphicsQuality)
            flags["DFIntDebugFRMQualityLevelOverride"] = QualityLevel(s.GraphicsQuality).ToString(CultureInfo.InvariantCulture);
        if (s.AntiAliasing >= 0)
            flags["FIntDebugForceMSAASamples"] = s.AntiAliasing.ToString(CultureInfo.InvariantCulture);
        if (s.TextureQuality >= 0)
        {
            flags["DFFlagTextureQualityOverrideEnabled"] = "True";
            flags["DFIntTextureQualityOverride"] = s.TextureQuality.ToString(CultureInfo.InvariantCulture);
        }
        if (s.IgnoreDisplayScaling)
            flags["DFFlagDisableDPIScale"] = "True";
        if (s.HideGrass)
        {
            flags["FIntFRMMinGrassDistance"] = "0";
            flags["FIntFRMMaxGrassDistance"] = "0";
            flags["FIntGrassMovementReducedMotionFactor"] = "0";
        }
        if (s.GraySky)
            flags["FFlagDebugSkyGray"] = "True";
        if (s.FreezeLighting)
            flags["DFFlagDebugPauseVoxelizer"] = "True";
        if (s.ExclusiveFullscreen)
            flags["FFlagHandleAltEnterFullscreenManually"] = "False";

        var skipped = new List<string>();
        string? error = MergeCustomFlags(s.CustomFastFlags, flags, skipped);
        return new FastFlagPlan(flags, skipped, error);
    }

    public static int QualityLevel(int step) =>
        (int)Math.Round(1 + (Math.Clamp(step, 1, AppSettings.MaxGraphicsQuality) - 1) * 20.0 / (AppSettings.MaxGraphicsQuality - 1));

    public static string Serialize(IReadOnlyDictionary<string, string> flags) =>
        JsonSerializer.Serialize(flags, new JsonSerializerOptions { WriteIndented = true });

    public int ApplyToVersions(IEnumerable<string> versionFolders)
    {
        FastFlagPlan plan = BuildPlan();
        bool apply = _settings.Settings.ApplyFastFlags && plan.Flags.Count > 0;
        string content = Serialize(plan.Flags);
        string hash = Hash(content);
        string? previousHash = _settings.Settings.AppliedFastFlagsHash;
        int written = 0;

        foreach (string folder in versionFolders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string path = Path.Combine(folder, "ClientSettings", SettingsFileName);
            try
            {
                if (apply)
                {
                    if (File.Exists(path) && string.Equals(Hash(File.ReadAllText(path)), hash, StringComparison.Ordinal))
                        continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, content, new UTF8Encoding(false));
                    written++;
                }
                else if (previousHash != null && File.Exists(path)
                         && string.Equals(Hash(File.ReadAllText(path)), previousHash, StringComparison.Ordinal))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        string? newHash = apply ? hash : null;
        if (!string.Equals(newHash, previousHash, StringComparison.Ordinal))
        {
            _settings.Settings.AppliedFastFlagsHash = newHash;
            _settings.Save();
        }

        return written;
    }

    public int? ReadFramerateCap()
    {
        try
        {
            if (!File.Exists(BasicSettingsPath))
                return null;
            XElement? element = FindFramerateCap(XDocument.Load(BasicSettingsPath));
            return element != null && int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }

    public bool ApplyFramerateCap()
    {
        int cap = _settings.Settings.FpsCap;
        if (cap <= 0 || !File.Exists(BasicSettingsPath))
            return false;

        try
        {
            XDocument document = XDocument.Load(BasicSettingsPath, LoadOptions.PreserveWhitespace);
            XElement? element = FindFramerateCap(document);
            string value = cap.ToString(CultureInfo.InvariantCulture);
            if (element == null)
            {
                XElement? properties = document.Descendants("Properties").FirstOrDefault();
                if (properties == null)
                    return false;
                properties.Add(new XElement("int", new XAttribute("name", "FramerateCap"), value));
            }
            else if (element.Value == value)
            {
                return true;
            }
            else
            {
                element.Value = value;
            }

            using var writer = new StreamWriter(BasicSettingsPath, false, new UTF8Encoding(false));
            document.Save(writer, SaveOptions.DisableFormatting);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return false;
        }
    }

    public void PrepareLaunch(RobloxVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        ApplyToVersions(new[] { version.FolderPath });
        ApplyFramerateCap();
    }

    private static XElement? FindFramerateCap(XDocument document) =>
        document.Descendants().FirstOrDefault(e => (string?)e.Attribute("name") == "FramerateCap");

    private static string? MergeCustomFlags(string? json, IDictionary<string, string> flags, List<string> skipped)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        if (json.Length > MaxCustomFlagsCharacters)
            return "The custom flags are too long.";

        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return "Custom flags must be a JSON object, like {\"FFlagDebugSkyGray\": \"True\"}.";

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                string? value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "True",
                    JsonValueKind.False => "False",
                    _ => null
                };

                if (value == null || !Allowed.Contains(property.Name))
                    skipped.Add(property.Name);
                else
                    flags[property.Name] = value;
            }
            return null;
        }
        catch (JsonException)
        {
            return "The custom flags aren't valid JSON.";
        }
    }

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.Replace("\r\n", "\n"))));
}
