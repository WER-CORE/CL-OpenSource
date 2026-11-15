using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Controls;

public partial class AccountCard : UserControl
{
    public AccountCard()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SettingsPageViewModel.LanguageConfigurationChanged += OnLanguageChanged;
        ApplyLocalization();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SettingsPageViewModel.LanguageConfigurationChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged() => Dispatcher.UIThread.Post(ApplyLocalization);

    public void ApplyLocalization()
    {
        if (DeleteAccountBtn != null)
        {
            ToolTip.SetTip(DeleteAccountBtn, LocalizationManager.GetString("Modpacks.DeleteBtn", "Видалити"));
        }
    }
}
