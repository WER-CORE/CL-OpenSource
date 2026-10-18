using Avalonia.Controls;
using Avalonia.Input;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class DownloadProgressDialog : Window
{
    public DownloadProgressDialog()
    {
        InitializeComponent();
    }

    public void SetViewModel(DownloadProgressViewModel vm)
    {
        vm.CloseAction = () => this.Close();
        DataContext = vm;
    }

    private void BorderTool_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }
}
