using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class ModCardViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private Bitmap? _icon;

    [ObservableProperty]
    private bool _isEnabled = true;

    [RelayCommand]
    private void ToggleMod()
    {

    }

    [RelayCommand]
    private async Task DeleteAsync()
    {

    }
}
