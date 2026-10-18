using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;

namespace CL_CLegendary_Launcher_.Avalonia.Models;

public class FundraiserData3rd
{
    public bool isActive { get; set; }
    public string title { get; set; } = string.Empty;
    public string imageUrl { get; set; } = string.Empty;
    public string fundUrl { get; set; } = string.Empty;
    public string detailsUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public Bitmap? ImageBitmap { get; set; }

    public string SupportText => "Підтримати";
    public string DetailsText => "Детальніше";

}
