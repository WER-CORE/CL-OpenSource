using Avalonia.Media.Imaging;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class ModpackCardViewModel : ObservableObject
{
    private static readonly string[] IconFileNames =
    [
        "icon.png", "icon.jpg", "icon.jpeg", "instance.png", "logo.png", "pack.png"
    ];

    public InstalledModpack? Modpack { get; set; }
    public Action<InstalledModpack>? OnPlay { get; set; }
    public Action<InstalledModpack>? OnDelete { get; set; }
    public Action<InstalledModpack>? OnOpenFolder { get; set; }
    public Action<InstalledModpack>? OnExport { get; set; }
    public Action<InstalledModpack>? OnEdit { get; set; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private Bitmap? _icon;
    [ObservableProperty] private bool _isSettingsVisible;

    public ModpackCardViewModel()
    {
    }

    public ModpackCardViewModel(
        InstalledModpack pack,
        Action<InstalledModpack>? onPlay = null,
        Action<InstalledModpack>? onDelete = null,
        Action<InstalledModpack>? onOpenFolder = null,
        Action<InstalledModpack>? onEdit = null,
        Action<InstalledModpack>? onShare = null)
    {
        Modpack = pack;
        Name = pack.Name ?? string.Empty;
        Description = $"{pack.LoaderType} {pack.MinecraftVersion} : {pack.LoaderVersion}";
        OnPlay = onPlay;
        OnDelete = onDelete;
        OnOpenFolder = onOpenFolder;
        OnEdit = onEdit;
        OnExport = onShare;

        _ = LoadIconAsync(pack);
    }

    private async Task LoadIconAsync(InstalledModpack pack)
    {
        Bitmap? iconBitmap = null;

        if (!string.IsNullOrWhiteSpace(pack.UrlImage))
        {
            iconBitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(pack.UrlImage, 90);
        }

        if (iconBitmap == null && !string.IsNullOrWhiteSpace(pack.Path))
        {
            string? localIconPath = await Task.Run(() => FindLocalIconPath(pack.Path));
            if (!string.IsNullOrEmpty(localIconPath))
            {
                iconBitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(localIconPath, 90);
            }
        }

        iconBitmap ??= await AvaloniaImageHelper.LoadOptimizedImageAsync("avares://CL.Avalonia/Assets/IconCL(Common).webp", 90)
                    ?? await AvaloniaImageHelper.LoadOptimizedImageAsync("avares://CL.Avalonia/Icon/IconCL(Common).webp", 90);

        Icon = iconBitmap;
    }

    private static string? FindLocalIconPath(string packPath)
    {
        if (!Directory.Exists(packPath))
            return null;

        string[] searchDirs =
        [
            packPath,
            Path.Combine(packPath, "overrides"),
            Path.Combine(packPath, "override")
        ];

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var files = Directory.GetFiles(dir);
                foreach (var iconName in IconFileNames)
                {
                    var match = Array.Find(files, f =>
                        string.Equals(Path.GetFileName(f), iconName, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                        return match;
                }
            }
            catch
            {

            }
        }

        return null;
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsVisible = !IsSettingsVisible;
    }

    [RelayCommand]
    private void Play()
    {
        if (Modpack != null) OnPlay?.Invoke(Modpack);
    }

    [RelayCommand]
    private void Delete()
    {
        if (Modpack != null) OnDelete?.Invoke(Modpack);
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (Modpack != null) OnOpenFolder?.Invoke(Modpack);
    }

    [RelayCommand]
    private void ExportZip()
    {
        if (Modpack != null) OnExport?.Invoke(Modpack);
    }

    [RelayCommand]
    private void Edit()
    {
        if (Modpack != null) OnEdit?.Invoke(Modpack);
    }
}
