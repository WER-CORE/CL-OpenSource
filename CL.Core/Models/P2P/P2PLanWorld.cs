using System;

namespace CL.Core.Models.P2P;

public class P2PLanWorld
{
    public string WorldName { get; set; } = string.Empty;
    public string Motd => WorldName;
    public int Port { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public string? ModpackName { get; set; }
    public string? ModpackTypeSite { get; set; }
    public string? GameVersion { get; set; }
    public string? LoaderType { get; set; }
    public string? LoaderVersion { get; set; }
    public string? ModsDirectory { get; set; }

    public string DisplayTitle
    {
        get
        {
            string versionInfo = string.Empty;
            if (!string.IsNullOrWhiteSpace(GameVersion))
            {
                string loader = !string.IsNullOrWhiteSpace(LoaderType) && !string.Equals(LoaderType, "Vanilla", StringComparison.OrdinalIgnoreCase)
                    ? LoaderType
                    : string.Empty;
                string loaderVer = !string.IsNullOrWhiteSpace(LoaderVersion) ? $" {LoaderVersion}" : string.Empty;
                versionInfo = !string.IsNullOrEmpty(loader) ? $" [{loader}{loaderVer} {GameVersion}]" : $" [{GameVersion}]";
            }

            if (!string.IsNullOrWhiteSpace(ModpackName))
                return $"{ModpackName} ({WorldName}){versionInfo}";
            return string.IsNullOrWhiteSpace(WorldName) ? $"Порт {Port}{versionInfo}" : $"{WorldName}{versionInfo}";
        }
    }
}
