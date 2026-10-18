using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using System;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class SelectedModItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Bitmap? _icon;

    public ModInfo Mod { get; }
    private readonly Action<SelectedModItemViewModel>? _onDelete;

    public SelectedModItemViewModel(ModInfo mod, Action<SelectedModItemViewModel>? onDelete)
    {
        Mod = mod;
        _onDelete = onDelete;
        Name = mod.Name ?? string.Empty;

        _ = LoadIconAsync(mod.ImageURL);
    }

    private async Task LoadIconAsync(string? iconUrl)
    {
        if (!string.IsNullOrEmpty(iconUrl))
        {
            Icon = await AvaloniaImageHelper.LoadOptimizedImageAsync(iconUrl, 32);
        }
        if (Icon == null)
        {
            Icon = await AvaloniaImageHelper.LoadOptimizedImageAsync("avares://CL.Avalonia/Assets/IconCL(Common).png", 32);
        }
    }

    [RelayCommand]
    private void Delete()
    {
        _onDelete?.Invoke(this);
    }
}
