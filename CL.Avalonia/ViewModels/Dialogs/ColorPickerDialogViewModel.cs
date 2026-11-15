using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CL.Core.Services;
using System;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

public partial class ColorPickerDialogViewModel : ObservableObject
{
    public Action<bool> CloseAction { get; set; }

    [ObservableProperty]
    private Color _selectedColor;

    [ObservableProperty]
    private string _cancelButtonText = string.Empty;

    [ObservableProperty]
    private string _applyButtonText = string.Empty;

    public ColorPickerDialogViewModel(Color initialColor)
    {
        SelectedColor = initialColor;
        UpdateLocalization();
    }

    private void UpdateLocalization()
    {
        CancelButtonText = LocalizationManager.GetString("ColorPicker.Cancel", "Скасувати");
        ApplyButtonText = LocalizationManager.GetString("ColorPicker.Apply", "Застосувати");
    }

    [RelayCommand]
    private void Apply()
    {
        CloseAction?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseAction?.Invoke(false);
    }
}
