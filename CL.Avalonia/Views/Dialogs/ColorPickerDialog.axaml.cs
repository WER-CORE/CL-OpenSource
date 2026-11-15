using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class ColorPickerDialog : Window
{
    private Color _selectedColor;
    public Color SelectedColor => DataContext is ColorPickerDialogViewModel vm ? vm.SelectedColor : _selectedColor;
    public bool DialogResult { get; private set; }

    public ColorPickerDialog()
    {
        InitializeComponent();
        this.EnableAutoMemoryCleanup();
    }

    public ColorPickerDialog(Color initialColor)
    {
        _selectedColor = initialColor;
        InitializeComponent();
        this.EnableAutoMemoryCleanup();

        var vm = new ColorPickerDialogViewModel(initialColor);
        vm.CloseAction = (result) =>
        {
            _selectedColor = vm.SelectedColor;
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
