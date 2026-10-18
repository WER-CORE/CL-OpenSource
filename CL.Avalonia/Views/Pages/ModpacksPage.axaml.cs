using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;
using CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Pages;

public partial class ModPacksPage : UserControl
{
    public static readonly StyledProperty<Control?> BackgroundVisualProperty =
        AvaloniaProperty.Register<ModPacksPage, Control?>(nameof(BackgroundVisual));

    public Control? BackgroundVisual
    {
        get => GetValue(BackgroundVisualProperty);
        set => SetValue(BackgroundVisualProperty, value);
    }

    public ModPacksPage()
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
        if (Loc_TxtModpackFilterTitle != null) Loc_TxtModpackFilterTitle.Text = LocalizationManager.GetString("Modpacks.FilterTitle", "Фільтр Збірок");
        if (SearchSystemModsTXT1 != null) SearchSystemModsTXT1.PlaceholderText = LocalizationManager.GetString("Generic.Search", "Пошук...");
        if (ImportTXT != null) ImportTXT.Text = LocalizationManager.GetString("Modpacks.ImportBtn", "Імпортувати");
        if (ImportModPacks != null) ToolTip.SetTip(ImportModPacks, LocalizationManager.GetString("Modpacks.ToolTipImport", "Натисніть, щоб імпортувати мод-пак збірки"));
        if (CreateModPacksTXT != null) CreateModPacksTXT.Text = LocalizationManager.GetString("Modpacks.CreateNewBtn", "Створити нову");
        if (DowloadModPacks != null) ToolTip.SetTip(DowloadModPacks, LocalizationManager.GetString("Modpacks.ToolTipCreate", "Натисніть, щоб створити власну мод-пак збірку"));
    }

    public void SearchSystemModsTXT1_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is ModpacksPageViewModel vm && sender is TextBox tb)
        {
            vm.ModpackSearchText = tb.Text ?? string.Empty;
        }
    }

    public void SearchSystemModsTXT1_PreviewMouseDown(object? sender, PointerPressedEventArgs e) { }

    public async void ImportTXT_MouseDown(object? sender, PointerPressedEventArgs e) =>
        await OpenModpackDialogAsync<DownloadModpackDialog, DownloadModpackDialogViewModel>();

    public async void DowloadModPacks_MouseDown(object? sender, PointerPressedEventArgs e) =>
        await OpenModpackDialogAsync<CreateModpackDialog, CreateModpackViewModel>();

    private async System.Threading.Tasks.Task OpenModpackDialogAsync<TDialog, TViewModel>()
        where TDialog : Window, new()
        where TViewModel : class
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new TDialog();
        if (Application.Current is App { ServiceProvider: { } sp })
        {
            dialog.DataContext = sp.GetRequiredService<TViewModel>();
        }

        await dialog.ShowDialog(owner);

        if (DataContext is ModpacksPageViewModel vm)
        {
            await vm.RefreshModpacksListAsync();
        }
    }
}
