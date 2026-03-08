using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Models
{
    public class ProfileItem
    {
        public string NameAccount { get; set; }
        public string UUID { get; set; }
        public string AccessToken { get; set; }
        public string ImageUrl { get; set; }
        public int Index { get; set; }
        public AccountType TypeAccount { get; set; }
    }

}
