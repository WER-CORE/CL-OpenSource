using CL_CLegendary_Launcher_.Class;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Models
{
    public class ModVersion
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("version_number")]
        public string VersionNumber { get; set; }

        [JsonProperty("game_versions")]
        public List<string> GameVersions { get; set; } = new();

        [JsonProperty("loaders")]
        public List<string> Loaders { get; set; } = new();

        [JsonProperty("files")]
        public List<ModFile> Files { get; set; } = new();
    }

}
