using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CL.Core.Models;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class AccountCardViewModel : ObservableObject
{
    private readonly Action<ProfileItem>? _onDelete;
    private readonly Action<ProfileItem>? _onSelect;

    public ProfileItem? Profile { get; }

    public event EventHandler? SelectClicked;
    public event EventHandler? DeleteClicked;

    [ObservableProperty] private string _nameAccount = string.Empty;
    [ObservableProperty] private string _uuid = string.Empty;
    [ObservableProperty] private string _accessToken = string.Empty;
    [ObservableProperty] private string _imageUrl = string.Empty;
    [ObservableProperty] private int _index;
    [ObservableProperty] private AccountType _typeAccount;
    [ObservableProperty] private Bitmap? _iconBitmap;

    public AccountCardViewModel()
    {
    }

    public AccountCardViewModel(
        ProfileItem profile,
        int index,
        Action<ProfileItem>? onDelete = null,
        Action<ProfileItem>? onSelect = null)
    {
        Profile = profile;
        _onDelete = onDelete;
        _onSelect = onSelect;

        NameAccount = profile.NameAccount ?? string.Empty;
        Uuid = profile.UUID ?? string.Empty;
        AccessToken = profile.AccessToken ?? string.Empty;
        ImageUrl = profile.ImageUrl ?? string.Empty;
        Index = index;
        TypeAccount = (AccountType)profile.TypeAccount;

        _ = LoadAvatarAsync(profile.ImageUrl);
    }

    private async Task LoadAvatarAsync(string? url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            IconBitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(url, 64);
        }
    }

    [RelayCommand]
    private void Select()
    {
        AvaloniaSoundService.Click();
        if (Profile != null)
        {
            _onSelect?.Invoke(Profile);
        }
        SelectClicked?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Delete()
    {
        AvaloniaSoundService.Click();
        if (Profile != null)
        {
            _onDelete?.Invoke(Profile);
        }
        DeleteClicked?.Invoke(this, EventArgs.Empty);
    }
}
