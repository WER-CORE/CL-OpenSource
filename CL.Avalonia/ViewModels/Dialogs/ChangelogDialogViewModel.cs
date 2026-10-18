using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

public partial class ChangelogDialogViewModel : ObservableObject
{
    private readonly string _versionId;
    private readonly string _versionType;

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private string _loadingText = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _markdownContent = string.Empty;

    [ObservableProperty]
    private bool _isMarkdownVisible = false;

    [ObservableProperty]
    private bool _isWikiFallbackVisible = false;

    [ObservableProperty]
    private string _wikiFallbackText = string.Empty;

    [ObservableProperty]
    private string _openWikiButtonText = string.Empty;

    public Action? CloseAction { get; set; }

    public ChangelogDialogViewModel(string versionId, string versionType)
    {
        _versionId = versionId;
        _versionType = versionType;

        ApplyLocalization();
        _ = LoadChangelogAsync();
    }

    private void ApplyLocalization()
    {
        WindowTitle = string.Format(LocalizationManager.GetString("ChangelogWindow.UpdateTitle", "Оновлення: {0}"), _versionId);
        WikiFallbackText = LocalizationManager.GetString("ChangelogWindow.OldVersionFallback", "Ця версія занадто стара або це снапшот, якого немає в офіційному архіві Mojang.");
        OpenWikiButtonText = LocalizationManager.GetString("ChangelogWindow.OpenWikiButton", "Читати на Minecraft Wiki");
    }

    private async Task LoadChangelogAsync()
    {
        string currentLang = SettingsManager.Default.LanguageCode;
        string cacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "Changelogs");
        Directory.CreateDirectory(cacheFolder);
        string cacheFile = Path.Combine(cacheFolder, $"{_versionId}_{currentLang}.md");

        try
        {
            if (File.Exists(cacheFile))
            {
                string cachedMarkdown = await File.ReadAllTextAsync(cacheFile);
                MarkdownContent = cachedMarkdown;
                IsLoading = false;
                IsMarkdownVisible = true;
                return;
            }

            ChangelogService changelogService = new ChangelogService();
            LoadingText = string.Format(LocalizationManager.GetString("ChangelogWindow.LoadingDescription", "Завантаження опису для {0}..."), _versionId);

            string englishMarkdown = await Task.Run(async () =>
                await changelogService.GetFullChangelogMarkdownAsync(_versionId)
            );

            if (englishMarkdown == null)
            {
                IsLoading = false;
                IsWikiFallbackVisible = true;
                return;
            }

            string finalMarkdown;

            if (currentLang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                finalMarkdown = englishMarkdown;
            }
            else
            {
                LoadingText = LocalizationManager.GetString("ChangelogWindow.Translating", "Перекладаю текст...\n(Це може зайняти кілька секунд, заварюй чай)");

                finalMarkdown = await Task.Run(async () =>
                    await changelogService.TranslateTextAsync(englishMarkdown, currentLang)
                );
            }

            await File.WriteAllTextAsync(cacheFile, finalMarkdown);

            MarkdownContent = finalMarkdown;
            IsLoading = false;
            IsMarkdownVisible = true;
        }
        catch (Exception ex)
        {
            IsLoading = false;
            IsMarkdownVisible = true;
            MarkdownContent = $"# Йой, сталася помилка!\n\n**Деталі:** {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenWiki()
    {
        string query;
        string cleanId = _versionId;

        switch (_versionType)
        {
            case "old_alpha": query = $"Java Edition Alpha {cleanId.Replace("a", "")}"; break;
            case "old_beta": query = $"Java Edition Beta {cleanId.Replace("b", "")}"; break;
            case "snapshot": query = $"{cleanId} Java Edition"; break;
            case "release": default: query = $"Java Edition {cleanId}"; break;
        }

        string currentLang = SettingsManager.Default.LanguageCode;

        string baseUrl = currentLang.StartsWith("uk", StringComparison.OrdinalIgnoreCase)
            ? "https://uk.minecraft.wiki"
            : "https://minecraft.wiki";

        string searchUrl = $"{baseUrl}/w/Special:Search?search={Uri.EscapeDataString(query)}&go=Go";

        UrlOpener.Open(searchUrl);
    }

    [RelayCommand]
    private void CloseWindow() => CloseAction?.Invoke();
}
