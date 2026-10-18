using System.Text.Json.Serialization;

namespace CL_CLegendary_Launcher_.Avalonia.Models;

public class MainThemeConfig
{
    [JsonPropertyName("SectionColor")]
    public string? SectionColor { get; set; }

    [JsonPropertyName("BackgroundColor")]
    public string? BackgroundColor { get; set; }

    [JsonPropertyName("AdditionalColor")]
    public string? AdditionalColor { get; set; }

    [JsonPropertyName("TextColor")]
    public string? TextColor { get; set; }

    [JsonPropertyName("ButtonColor")]
    public string? ButtonColor { get; set; }

    [JsonPropertyName("BackgroundImage")]
    public string? BackgroundImage { get; set; }
}

public class LoadScreenConfig
{
    [JsonPropertyName("LoadScreenColor")]
    public string? LoadScreenColor { get; set; }

    [JsonPropertyName("LoadScreenBg")]
    public string? LoadScreenBg { get; set; }
}
