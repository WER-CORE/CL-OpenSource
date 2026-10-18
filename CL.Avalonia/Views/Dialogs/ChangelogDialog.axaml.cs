using Avalonia.Controls;
using Avalonia.Input;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class ChangelogDialog : Window
{

    public ChangelogDialog()
    {
        InitializeComponent();
    }

    public ChangelogDialog(string versionId, string versionType)
    {
        InitializeComponent();

        var vm = new ChangelogDialogViewModel(versionId, versionType);
        vm.CloseAction = () => this.Close();
        DataContext = vm;
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}
