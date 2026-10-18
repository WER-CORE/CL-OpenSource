using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class EditModpackItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private Bitmap? _icon;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _filePath = string.Empty;

    private readonly Action<EditModpackItemViewModel> _onToggle;
    private readonly Action<EditModpackItemViewModel> _onDelete;

    public EditModpackItemViewModel(Action<EditModpackItemViewModel> onToggle, Action<EditModpackItemViewModel> onDelete)
    {
        _onToggle = onToggle;
        _onDelete = onDelete;
    }

    partial void OnIsEnabledChanged(bool value)
    {

        _onToggle?.Invoke(this);
    }

    [RelayCommand]
    private void Delete()
    {
        _onDelete?.Invoke(this);
    }
}
