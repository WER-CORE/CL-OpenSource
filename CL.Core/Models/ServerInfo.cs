using System;

namespace CL.Core.Models
{
    public class ServerInfo
    {
        public string Name { get; set; }
        public string Ip { get; set; }
        public int Port { get; set; }
        public string Version { get; set; }
        public string Type { get; set; }
        public bool IsPartner { get; set; }
        public int Priority { get; set; }
        public string BorderColorHex { get; set; }
        public string TextColorHex { get; set; }
        public bool NeonEffect { get; set; }
        public string DiscordLink { get; set; }
        public string DonateLink { get; set; }
        public string SiteLink { get; set; }
        public string Description { get; set; }
        public string LogoUrl { get; set; }
        public string BgUrl { get; set; }
        public System.Collections.Generic.List<string> VersionsSupported { get; set; }        
    }
}
