using CL_CLegendary_Launcher_.Class;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Models
{
    public class CustomModpackManifest
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Minecraft { get; set; }
        public string Loader { get; set; }
        public string LoaderVersion { get; set; }
        public List<ModInfo> Files { get; set; } = new List<ModInfo>();
    }
}
