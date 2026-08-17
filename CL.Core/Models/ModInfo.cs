using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public class ModInfo
    {
        public int Index { get; set; }
        public string ImageURL { get; set; }
        public string Name { get; set; }
        public string ProjectId { get; set; }
        public string FileId { get; set; }
        public string Loader { get; set; }
        public string LoaderType { get; set; }
        public string Version { get; set; }
        public string Url { get; set; }
        public string Type { get; set; } 
        public string Slug { get; set; }
        public string FileName { get; set; }
    }
    public class ModFile
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("primary")]
        public bool Primary { get; set; }
    }
}
