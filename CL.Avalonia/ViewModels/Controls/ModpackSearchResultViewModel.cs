using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL.Core.Services;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class ModpackSearchResultViewModel : ObservableObject
{
    public string ProjectId { get; }
    public string Name { get; }
    public string Slug { get; }
    public string Site { get; }
    public string OriginalIconUrl { get; }

    [ObservableProperty]
    private Bitmap? _icon;

    public ModpackSearchResultViewModel(string id, string name, string slug, string site, string iconUrl)
    {
        ProjectId = id;
        Name = name;
        Slug = slug;
        Site = site;
        OriginalIconUrl = iconUrl;
        _ = LoadIconAsync(iconUrl);
    }

    private async Task LoadIconAsync(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            Icon = await AvaloniaImageHelper.LoadOptimizedImageAsync(url, 100);
        }
        if (Icon == null)
        {
            Icon = await AvaloniaImageHelper.LoadOptimizedImageAsync("avares://CL.Avalonia/Assets/IconCL(Common).png", 100);
        }
    }

    [RelayCommand]
    private void OpenDetails()
    {
        AvaloniaSoundService.Click();
        if (Site == "Modrinth")
        {
            WebHelper.OpenUrl($"https://modrinth.com/modpack/{Slug}");
        }
        else
        {
            WebHelper.OpenUrl($"https://www.curseforge.com/minecraft/modpacks/{Slug}");
        }
    }
}
