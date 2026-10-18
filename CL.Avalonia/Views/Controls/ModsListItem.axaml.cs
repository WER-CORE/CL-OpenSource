using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Controls;

public partial class ModsListItem : UserControl
{
    public ModsListItem()
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
        if (ToolTipDownloads != null)
            ToolTip.SetTip(ToolTipDownloads, LocalizationManager.GetString("Mods.ItemJarDownloadsTooltip", "Кількість завантажень"));
        if (ToolTipUpdate != null)
            ToolTip.SetTip(ToolTipUpdate, LocalizationManager.GetString("Mods.ItemJarUpdateTooltip", "Останнє оновлення"));
        if (ToolTipAuthor != null)
            ToolTip.SetTip(ToolTipAuthor, LocalizationManager.GetString("Mods.ItemJarAuthorTooltip", "Автор"));
        if (TxtInstall != null)
            TxtInstall.Text = LocalizationManager.GetString("Mods.ItemJarInstallBtn", "Встановити");
        if (TxtDetails != null)
            TxtDetails.Text = LocalizationManager.GetString("Mods.ItemJarDetailsBtn", "Докладніше");
    }
}
