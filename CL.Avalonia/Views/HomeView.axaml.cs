using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using CL_CLegendary_Launcher_.Avalonia.ViewModels;

namespace CL_CLegendary_Launcher_.Avalonia.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
        }

        // Для OptiFine після версії ще треба обрати збірку, тож список лишається відкритим.
        private void OnVersionTapped(object? sender, TappedEventArgs e)
        {
            bool onItem = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true) != null;

            if (onItem && DataContext is HomeViewModel { IsOptifine: false })
                VersionButton.Flyout?.Hide();
        }
    }
}
