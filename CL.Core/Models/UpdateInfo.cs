using System.Text.Json.Serialization;

namespace CL.Core.Models
{
    public class UpdateInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("url")]
        public string UrlDefault { get; set; } = "";

        [JsonPropertyName("url_x86")]
        public string UrlX86 { get; set; } = "";

        [JsonPropertyName("url_linux_x64")]
        public string UrlLinuxX64 { get; set; } = "";

        [JsonPropertyName("url_linux_arm64")]
        public string UrlLinuxArm64 { get; set; } = "";

        [JsonPropertyName("url_osx_x64")]
        public string UrlOsxX64 { get; set; } = "";
        
        [JsonPropertyName("url_osx_arm64")]
        public string UrlOsxArm64 { get; set; } = "";
    }
}
