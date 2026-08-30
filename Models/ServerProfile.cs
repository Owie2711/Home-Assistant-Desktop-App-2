namespace HomeAssistantDesktop.Models;

public sealed class ServerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Home Assistant";
    public string Url { get; set; } = "http://homeassistant.local:8123";
    public bool IsDefault { get; set; }
}
