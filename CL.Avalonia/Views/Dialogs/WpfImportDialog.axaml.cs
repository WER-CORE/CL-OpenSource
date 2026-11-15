using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class WpfImportDialog : Window
{
    private string? _selectedFolder;
    private LegacyDataInspectionResult? _inspection;

    public bool WasImported { get; private set; }
    public bool WasDeclined { get; private set; }

    public WpfImportDialog() : this(null)
    {
    }

    public WpfImportDialog(string? initialCandidatePath)
    {
        InitializeComponent();
        ApplyLocalization();
        if (!string.IsNullOrWhiteSpace(initialCandidatePath))
        {
            InspectFolder(initialCandidatePath);
        }
        else
        {
            UpdateImportButtonState();
        }
    }

    private void ApplyLocalization()
    {
        Title = LocalizationManager.GetString("Settings.ImportLegacyTitle", "Імпорт даних");
        TitleText.Text = LocalizationManager.GetString("Settings.ImportWpfTitle", "Бажаєте імпортувати дані з попередньої версії Clegendary Launcher?");
        SubtitleText.Text = LocalizationManager.GetString("Settings.ImportWpfSubtitle", "Ви можете перенести налаштування та інші підтримувані дані зі старого CL. Оригінальні файли залишаться без змін.");
        SelectedPathText.Text = LocalizationManager.GetString("Settings.ImportWpfNoFolderSelected", "Папку не вибрано");
        BrowseFolderBtn.Content = LocalizationManager.GetString("Settings.ImportWpfBrowseBtn", "Обрати папку...");
        PreviewHeaderText.Text = LocalizationManager.GetString("Settings.ImportWpfPreviewHeader", "Попередній перегляд даних:");
        ConfirmOverwriteCheckBox.Content = LocalizationManager.GetString("Settings.ImportWpfConfirmOverwrite", "Підтверджую перезапис наявних файлів (перед імпортом буде створено резервну копію)");
        DeclineBtn.Content = LocalizationManager.GetString("Settings.ImportWpfDeclineBtn", "Ні, дякую");
        ImportBtn.Content = LocalizationManager.GetString("Settings.ImportWpfImportBtn", "Імпортувати");
    }

    private async void BrowseFolderBtn_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        var dialogService = AvaloniaDialogService.Current;
        if (dialogService == null) return;

        string? picked = await dialogService.OpenFolderDialogAsync(
            LocalizationManager.GetString("Settings.ImportLegacyDialogTitle", "Виберіть папку Data або папку зі старим Clegendary Launcher"));

        if (!string.IsNullOrWhiteSpace(picked))
        {
            InspectFolder(picked);
        }
    }

    private void InspectFolder(string folderPath)
    {
        _selectedFolder = folderPath;
        SelectedPathText.Text = folderPath;
        _inspection = LegacyMigrationService.InspectLegacyDataFolder(folderPath);

        ErrorText.IsVisible = false;
        WarningText.IsVisible = false;
        ConfirmOverwriteCheckBox.IsVisible = false;
        ConfirmOverwriteCheckBox.IsChecked = false;

        if (!_inspection.IsValid)
        {
            PreviewBox.IsVisible = false;
            ErrorText.Text = _inspection.ErrorMessage ?? LocalizationManager.GetString("Settings.ImportWpfInspectError", "Не вдалося перевірити вибрану папку.");
            ErrorText.IsVisible = true;
            UpdateImportButtonState();
            return;
        }

        string settingsStatus = _inspection.HasSettings
            ? LocalizationManager.GetString("Settings.ImportWpfPreviewFound", "знайдено")
            : LocalizationManager.GetString("Settings.ImportWpfPreviewMissing", "відсутні");

        var lines = new List<string>
        {
            string.Format(LocalizationManager.GetString("Settings.ImportWpfPreviewPath", "Шлях Data: {0}"), _inspection.ResolvedDataPath),
            string.Format(LocalizationManager.GetString("Settings.ImportWpfPreviewSettings", "Налаштування (UserSaves.json): {0}"), settingsStatus),
            string.Format(LocalizationManager.GetString("Settings.ImportWpfPreviewProfiles", "Профілів / акаунтів: {0}"), _inspection.ProfilesCount),
            string.Format(LocalizationManager.GetString("Settings.ImportWpfPreviewModpacks", "Збірок (installed_modpacks.json): {0}"), _inspection.ModpacksCount),
            string.Format(LocalizationManager.GetString("Settings.ImportWpfPreviewFiles", "Усього підтримуваних файлів: {0}"), _inspection.SupportedFilesCount)
        };
        PreviewDetailsText.Text = string.Join("\n", lines);
        PreviewBox.IsVisible = true;

        if (_inspection.CorruptedFiles.Count > 0)
        {
            ErrorText.Text = string.Format(
                LocalizationManager.GetString("Settings.ImportWpfCorruptedJson", "Виявлено пошкоджені файли JSON: {0}. Імпорт заблоковано для безпеки даних."),
                string.Join(", ", _inspection.CorruptedFiles));
            ErrorText.IsVisible = true;
        }

        if (_inspection.Conflicts.Count > 0)
        {
            WarningText.Text = string.Format(
                LocalizationManager.GetString("Settings.ImportWpfConflictsWarning", "Увага! У поточній папці Data вже є файли: {0}."),
                string.Join(", ", _inspection.Conflicts));
            WarningText.IsVisible = true;
            ConfirmOverwriteCheckBox.IsVisible = true;
        }

        UpdateImportButtonState();
    }

    private void ConfirmOverwriteCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        UpdateImportButtonState();
    }

    private void UpdateImportButtonState()
    {
        bool canImport = _inspection != null
                         && _inspection.IsValid
                         && _inspection.CorruptedFiles.Count == 0
                         && (_inspection.Conflicts.Count == 0 || ConfirmOverwriteCheckBox.IsChecked == true);

        ImportBtn.IsEnabled = canImport;
    }

    private void ImportBtn_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (string.IsNullOrWhiteSpace(_selectedFolder) || _inspection == null || !_inspection.IsValid)
            return;

        if (_inspection.Conflicts.Count > 0 && ConfirmOverwriteCheckBox.IsChecked != true)
            return;

        var result = LegacyMigrationService.ImportSupportedDataSafely(_selectedFolder);
        if (!result.Success)
        {
            ErrorText.Text = result.ErrorMessage ?? LocalizationManager.GetString("Settings.ImportWpfFailedRestore", "Під час імпорту сталася помилка. Попередні дані відновлено.");
            ErrorText.IsVisible = true;
            return;
        }

        SettingsManager.Default.WpfImportPromptHandled = true;
        SettingsManager.Save();

        var summary = new List<string>();
        if (result.ProfilesImported > 0)
            summary.Add(string.Format(LocalizationManager.GetString("Settings.ImportLegacyProfilesCount", "Профілів/акаунтів: {0}"), result.ProfilesImported));
        if (result.SettingsImported)
            summary.Add(LocalizationManager.GetString("Settings.ImportLegacySettingsDone", "Налаштування оновлено"));
        if (result.ModpacksImported > 0)
            summary.Add(string.Format(LocalizationManager.GetString("Settings.ImportLegacyModpacksCount", "Збірок перенесено: {0}"), result.ModpacksImported));
        if (result.FilesImported > 0)
            summary.Add(string.Format(LocalizationManager.GetString("Settings.ImportLegacyFilesCount", "Файлів із папки Data: {0}"), result.FilesImported));

        string successHeader = LocalizationManager.GetString("Settings.ImportLegacySuccess", "Папку Data успішно імпортовано!");
        AvaloniaDialogService.Show(
            summary.Count > 0 ? successHeader + "\n\n" + string.Join("\n", summary) : successHeader,
            LocalizationManager.GetString("Settings.ImportLegacyTitle", "Імпорт даних"),
            MascotEmotion.Happy);

        WasImported = true;
        Close();
    }

    private void DeclineBtn_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        SettingsManager.Default.WpfImportPromptHandled = true;
        SettingsManager.Save();
        WasDeclined = true;
        Close();
    }
}
