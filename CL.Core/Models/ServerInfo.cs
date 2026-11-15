namespace CL.Core.Models
{
    public class ServerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; } = 25565;
        public string Version { get; set; } = string.Empty;
        public int OnlinePlayers { get; set; }
        public string LogoUrl { get; set; } = string.Empty;
        public string AllMcUrl { get; set; } = string.Empty;
        public string SiteLink { get; set; } = string.Empty;
        public string DiscordLink { get; set; } = string.Empty;
    }
}
