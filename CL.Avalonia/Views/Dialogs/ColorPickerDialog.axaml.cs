using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class ColorPickerDialog : Window
{
    public Color SelectedColor => ((ColorPickerDialogViewModel)DataContext!).SelectedColor;
    public bool DialogResult { get; private set; }

    public ColorPickerDialog()
    {
        InitializeComponent();
    }

    public ColorPickerDialog(Color initialColor)
    {
        InitializeComponent();

        var vm = new ColorPickerDialogViewModel(initialColor);
        vm.CloseAction = (result) =>
        {
            DialogResult = result;
            this.Close(result);
        };
        DataContext = vm;
    }

    private void Window_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }
}
