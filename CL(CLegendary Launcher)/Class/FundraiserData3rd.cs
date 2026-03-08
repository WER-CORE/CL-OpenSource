using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace CL_CLegendary_Launcher_.Class
{
    public class FundraiserData3rd
    {
        public bool isActive { get; set; }
        public string title { get; set; }
        public string imageUrl { get; set; }
        public string fundUrl { get; set; }
        public string detailsUrl { get; set; }
        public string SupportText => LocalizationManager.GetString("Fundraisers.SupportBtn", "ПІДТРИМАТИ");
        public string DetailsText => LocalizationManager.GetString("Fundraisers.DetailsBtn", "ДЕТАЛЬНІШЕ");
        [JsonIgnore]
        public BitmapImage ImageBitmap { get; set; }
    }
}
