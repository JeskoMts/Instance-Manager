namespace InstanceManager.Models;

public sealed class ThemePalette
{
    public string Window { get; set; } = "#0A0A0A";
    public string Surface { get; set; } = "#151515";
    public string SurfaceHover { get; set; } = "#1E1E1E";
    public string SurfaceActive { get; set; } = "#262626";
    public string Elevated { get; set; } = "#2D2D2D";
    public string ElevatedHover { get; set; } = "#383838";

    public string Border { get; set; } = "#2C2C2C";
    public string BorderStrong { get; set; } = "#3D3D3D";

    public string Accent { get; set; } = "#EDEDED";
    public string OnAccent { get; set; } = "#0A0A0A";

    public string TextPrimary { get; set; } = "#F2F2F2";
    public string TextSecondary { get; set; } = "#B4B4B4";
    public string TextMuted { get; set; } = "#8C8C8C";

    public string Success { get; set; } = "#5B9E7E";
    public string Danger { get; set; } = "#C65F55";

    public ThemePalette Clone() => (ThemePalette)MemberwiseClone();
}
