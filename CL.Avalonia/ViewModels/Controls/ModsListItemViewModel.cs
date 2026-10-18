using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class ModsListItemViewModel : ObservableObject, IDisposable
{
    public ModSearchResult? Mod { get; }

    [ObservableProperty] private string _modTitle = string.Empty;
    [ObservableProperty] private string _modDescription = string.Empty;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _lastUpdateDate = string.Empty;
    [ObservableProperty] private string _downloadCount = "0";
    [ObservableProperty] private Bitmap? _modImage;

    public Action? OnInstall { get; set; }
    public Action? OnDetails { get; set; }

    public void Dispose()
    {
        ModImage = null;
        OnInstall = null;
        OnDetails = null;
    }

    public ModsListItemViewModel()
    {
    }

    public ModsListItemViewModel(
        ModSearchResult mod,
        Action<ModSearchResult>? onDownloadClick = null,
        Action<ModSearchResult>? onOpenUrlClick = null)
    {
        Mod = mod;
        ModTitle = mod.Title ?? string.Empty;
        ModDescription = mod.Description ?? string.Empty;
        Author = mod.Author ?? string.Empty;
        DownloadCount = FormatNumber(mod.Downloads);
        LastUpdateDate = mod.UpdatedDate ?? string.Empty;

        if (onDownloadClick != null)
            OnInstall = () => onDownloadClick(mod);

        if (onOpenUrlClick != null)
            OnDetails = () => onOpenUrlClick(mod);

        _ = LoadIconAsync(mod.IconUrl);
    }

    private async Task LoadIconAsync(string? iconUrl)
    {
        if (!string.IsNullOrEmpty(iconUrl))
        {
            ModImage = await AvaloniaImageHelper.LoadOptimizedImageAsync(iconUrl, 86);
        }
    }

    public static string FormatNumber(object? numObj)
    {
        if (numObj == null) return "0";

        if (numObj is string strVal)
        {
            strVal = strVal.Replace(" ", "").Replace(",", "").Replace(".", "").Trim();
            if (long.TryParse(strVal, out long parsed))
                return FormatNumber(parsed);
            return strVal;
        }

        if (numObj is long or int or double)
        {
            long num = Convert.ToInt64(numObj);
            if (num >= 1_000_000) return (num / 1_000_000D).ToString("0.#") + "m";
            if (num >= 1_000) return (num / 1_000D).ToString("0.#") + "k";
            return num.ToString();
        }

        return numObj.ToString() ?? "0";
    }

    [RelayCommand]
    private void Install()
    {
        OnInstall?.Invoke();
    }

    [RelayCommand]
    private void Details()
    {
        OnDetails?.Invoke();
    }
}
