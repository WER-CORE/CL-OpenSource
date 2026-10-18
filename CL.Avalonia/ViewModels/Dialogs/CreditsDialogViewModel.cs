using System;
using System.Threading.Tasks;
using Avalonia.Layout;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Layout;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

public partial class CreditsDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private string _creditsTitle = string.Empty;

    [ObservableProperty]
    private string _closeButtonText = string.Empty;

    [ObservableProperty]
    private string _creditsContent = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private HorizontalAlignment _contentHorizontalAlignment = HorizontalAlignment.Center;

    [ObservableProperty]
    private VerticalAlignment _contentVerticalAlignment = VerticalAlignment.Center;

    public Action? CloseAction { get; set; }

    public CreditsDialogViewModel()
    {
        ApplyLocalization();
        _ = LoadCreditsAsync();
    }

    private void ApplyLocalization()
    {
        WindowTitle = LocalizationManager.GetString("Credits.Title", "Автори та Спільнота");
        CreditsTitle = LocalizationManager.GetString("Credits.Title", "Автори та Спільнота");
        CloseButtonText = LocalizationManager.GetString("Credits.CloseBtn", "Закрити");
        CreditsContent = LocalizationManager.GetString("Credits.Loading", "Сіель шукає список наших героїв...");
    }

    private async Task LoadCreditsAsync()
    {
        try
        {
            IsLoading = true;
            string urlWithNoCache = $"{Secrets.CreditsUrl}?v={DateTime.Now.Ticks}";

            string creditsText = await WebHelper.Client.GetStringAsync(urlWithNoCache);

            CreditsContent = creditsText;
            ContentHorizontalAlignment = HorizontalAlignment.Left;
            ContentVerticalAlignment = VerticalAlignment.Top;
        }
        catch (Exception)
        {
            CreditsContent = LocalizationManager.GetString("Credits.LoadError", "Ой, не вдалося завантажити список з інтернету.\nАле ми все одно безмежно вдячні всім, хто нам допомагає! (´• ω •`)");
            ContentHorizontalAlignment = HorizontalAlignment.Center;
            ContentVerticalAlignment = VerticalAlignment.Center;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        AvaloniaSoundService.Click();
        CloseAction?.Invoke();
    }
}
