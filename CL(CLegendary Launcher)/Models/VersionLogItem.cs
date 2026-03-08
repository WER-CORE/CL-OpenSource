using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Models
{
    public class VersionLogItem
    {
        public string VersionId { get; set; }
        public string IconPath { get; set; }
        public string VersionType { get; set; }
        public override string ToString()
        {
            return VersionId;
        }
    }

}
