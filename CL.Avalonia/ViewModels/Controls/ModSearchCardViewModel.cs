using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using System;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class ModSearchCardViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Bitmap? _icon;

    public ModSearchResult Result { get; }
    private readonly Func<ModSearchResult, Task>? _onAdd;
    private readonly int _selectMod;

    public ModSearchCardViewModel()
    {
        Result = new ModSearchResult { Title = "Example Mod", Slug = "example" };
    }

    public ModSearchCardViewModel(ModSearchResult result, Func<ModSearchResult, Task>? onAdd, int selectMod)
    {
        Result = result;
        _onAdd = onAdd;
        _selectMod = selectMod;
        Name = result.Title ?? string.Empty;

        _ = LoadIconAsync(result.IconUrl);
    }

    private async Task LoadIconAsync(string? iconUrl)
    {
        if (!string.IsNullOrEmpty(iconUrl))
        {
            Icon = await AvaloniaImageHelper.LoadOptimizedImageAsync(iconUrl, 32);
        }
        if (Icon == null)
        {
            Icon = await AvaloniaImageHelper.LoadOptimizedImageAsync("avares://CL.Avalonia/Assets/IconCL(Common).webp", 32);
        }
    }

    [RelayCommand]
    private async Task AddModAsync()
    {
        if (_onAdd != null)
        {
            await _onAdd(Result);
        }
    }

    [RelayCommand]
    private void Details()
    {
        AvaloniaSoundService.Click();
        string baseUrl = Result.Site == "Modrinth" ? "https://modrinth.com" : "https://www.curseforge.com/minecraft";

        string category = Result.Site == "Modrinth"
            ? _selectMod switch
            {
                1 => "shader",
                2 => "resourcepack",
                4 => "datapack",
                _ => "mod"
            }
            : _selectMod switch
            {
                1 => "shaders",
                2 => "texture-packs",
                3 => "worlds",
                4 => "data-packs",
                _ => "mc-mods"
            };

        WebHelper.OpenUrl($"{baseUrl}/{category}/{Result.Slug}");
    }
}
