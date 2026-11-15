using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Controls;

public partial class ModpackCard : UserControl
{
    public ModpackCard()
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
        if (TxtSetting != null) TxtSetting.Text = LocalizationManager.GetString("Modpacks.SettingsBtn", "Налаштування");
        if (TxtPlay != null) TxtPlay.Text = LocalizationManager.GetString("Modpacks.PlayBtn", "Грати");
        if (TxtDelete != null) TxtDelete.Text = LocalizationManager.GetString("Modpacks.DeleteBtn", "Видалити");
        if (TxtFolder != null) TxtFolder.Text = LocalizationManager.GetString("Modpacks.FolderBtn", "Тека Збірки");
        if (TxtExport != null) TxtExport.Text = LocalizationManager.GetString("Modpacks.ExportZipBtn", "Експортувати ZIP");
        if (TxtEdit != null) TxtEdit.Text = LocalizationManager.GetString("Modpacks.EditBtn", "Редагувати");
    }
}
