using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Models
{
    public class ModpackInfo
    {
        public string Name { get; set; }
        public string TypeSite { get; set; }
        public string MinecraftVersion { get; set; }
        public string LoaderVersion { get; set; }
        public string LoaderType { get; set; }
        public string Path { get; set; }
        public string PathJson { get; set; }
        public string UrlImage { get; set; }
        public bool IsConsoleLogOpened { get; set; } = false;
        public int OPack { get; set; } = 4096;
        public int Wdith { get; set; } = 600;
        public int Height { get; set; } = 800;
        public bool EnterInServer { get; set; } = false;
        public string ServerIP { get; set; } = "Назва сервера";
    }

}
