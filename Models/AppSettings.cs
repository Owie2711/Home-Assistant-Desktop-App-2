using System.Text.Json.Serialization;

namespace HomeAssistantDesktop.Models;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool Fullscreen { get; set; }
    public bool StartMinimized { get; set; }
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 800;
    public bool WindowMaximized { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double ZoomFactor { get; set; } = 1.0;
    public string? ActiveServerId { get; set; }
    public List<ServerProfile> Servers { get; set; } = [];

    [JsonIgnore] public ServerProfile? ActiveServer =>
        Servers.FirstOrDefault(s => s.Id == ActiveServerId)
        ?? Servers.FirstOrDefault(s => s.IsDefault)
        ?? Servers.FirstOrDefault();
}
