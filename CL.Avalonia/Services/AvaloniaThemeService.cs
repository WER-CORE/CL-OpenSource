using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Models;
using CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class AvaloniaThemeService
{
    private readonly IDialogService _dialogService;
    private readonly IMainWindowController _windowController;

    public event Action<MainThemeConfig>? ThemeChanged;
    public event Action<Bitmap?>? BackgroundBitmapChanged;

    public string CurrentTheme { get; private set; } = "Dark";
    public MainThemeConfig? CurrentConfig { get; private set; }
    public Bitmap? CurrentBackgroundBitmap { get; private set; }

    public AvaloniaThemeService(IDialogService dialogService, IMainWindowController windowController)
    {
        _dialogService = dialogService;
        _windowController = windowController;
    }

    public void InitializeSavedTheme()
    {
        string savedTheme = string.IsNullOrWhiteSpace(SettingsManager.Default.Them)
            ? "Dark"
            : SettingsManager.Default.Them;

        ToggleGlassEffect(SettingsManager.Default.DisableGlassEffect);

        if (savedTheme.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            CurrentTheme = "Custom";
            var customConfig = new MainThemeConfig
            {
                SectionColor = string.IsNullOrWhiteSpace(SettingsManager.Default.Section_colour) ? "#FF303030" : SettingsManager.Default.Section_colour,
                BackgroundColor = string.IsNullOrWhiteSpace(SettingsManager.Default.Background_colour) ? "#FF202020" : SettingsManager.Default.Background_colour,
                AdditionalColor = string.IsNullOrWhiteSpace(SettingsManager.Default.Additional_colour) ? "#FF1863C9" : SettingsManager.Default.Additional_colour,
                TextColor = string.IsNullOrWhiteSpace(SettingsManager.Default.Text_colour) ? "#FFFFFFFF" : SettingsManager.Default.Text_colour,
                ButtonColor = string.IsNullOrWhiteSpace(SettingsManager.Default.Button_colour) ? "#FF202020" : SettingsManager.Default.Button_colour,
                BackgroundImage = SettingsManager.Default.bgImage
            };

            bool isLightCustom = IsColorLight(customConfig.BackgroundColor);
            ChangeBaseTheme(!isLightCustom);
            ApplyCustomTheme(customConfig);
        }
        else
        {
            ApplyPredefinedTheme(savedTheme);
        }
    }

    public void ApplyPredefinedTheme(string themeName)
    {
        CurrentTheme = themeName;
        SettingsManager.Default.Them = themeName;
        SettingsManager.Save();

        bool isDarkTheme = !string.Equals(themeName, "Light", StringComparison.OrdinalIgnoreCase);
        ChangeBaseTheme(isDarkTheme);

        MainThemeConfig presetConfig = themeName switch
        {
            "Ocean" => new MainThemeConfig
            {
                BackgroundColor = "#FF0F1E38",
                SectionColor = "#FF15274D",
                ButtonColor = "#FF1C3056",
                TextColor = "#FFFFFFFF",
                AdditionalColor = "#FF00D2FF",
                BackgroundImage = "avares://CL.Avalonia/Assets/Backgrounds/OceanBG.png"
            },
            "Forest" => new MainThemeConfig
            {
                BackgroundColor = "#FF122416",
                SectionColor = "#FF182E1E",
                ButtonColor = "#FF1E3B27",
                TextColor = "#FFFFFFFF",
                AdditionalColor = "#FF44D62C",
                BackgroundImage = "avares://CL.Avalonia/Assets/Backgrounds/ForestBG.png"
            },
            "Sunset" => new MainThemeConfig
            {
                BackgroundColor = "#FF2D1A10",
                SectionColor = "#FF3D1F10",
                ButtonColor = "#FF4A2616",
                TextColor = "#FFFFFFFF",
                AdditionalColor = "#FFFF6A00",
                BackgroundImage = "avares://CL.Avalonia/Assets/Backgrounds/SunSetBG.png"
            },
            "Light" => new MainThemeConfig
            {
                BackgroundColor = "#FFFFFFFF",
                SectionColor = "#FFD4D4D4",
                ButtonColor = "#FFE8E8E8",
                TextColor = "#FF0A0A0A",
                AdditionalColor = "#FF1863C9",
                BackgroundImage = "avares://CL.Avalonia/Assets/Backgrounds/LightThemBG.png"
            },
            _ => new MainThemeConfig
            {
                BackgroundColor = "#FF202020",
                SectionColor = "#FF303030",
                ButtonColor = "#FF202020",
                TextColor = "#FFFFFFFF",
                AdditionalColor = "#FF1863C9",
                BackgroundImage = "avares://CL.Avalonia/Assets/Backgrounds/DarkThemBG.png"
            }
        };

        ApplyCustomTheme(presetConfig);
    }

    public void ChangeBaseTheme(bool isDark)
    {
        if (Application.Current != null)
        {

            Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    public void ApplyCustomTheme(MainThemeConfig config)
    {
        CurrentConfig = config;
        var resources = Application.Current?.Resources;
        if (resources == null) return;

        if (CurrentTheme == "Custom")
        {
            bool isLightCustom = IsColorLight(config.BackgroundColor);
            ChangeBaseTheme(!isLightCustom);
        }

        UpdateResource(resources, "MainBackgroundBrushServer", config.SectionColor);
        UpdateResource(resources, "MainBackgroundBrush", config.BackgroundColor);
        UpdateResource(resources, "MainBackgroundProgressBar", config.AdditionalColor);
        UpdateResource(resources, "MainForegroundBrush", config.TextColor);
        UpdateResource(resources, "MainBorderBrush", config.TextColor);
        UpdateResource(resources, "MainBackgroundButton", config.ButtonColor);

        if (Color.TryParse(NormalizeHex(config.BackgroundColor), out var bgColor))
        {
            double opacity = SettingsManager.Default.DisableGlassEffect ? 1.0 : 0.6;
            resources["GlassTintBrush"] = new SolidColorBrush(bgColor, opacity);
        }

        if (!string.IsNullOrEmpty(config.SectionColor)) SettingsManager.Default.Section_colour = config.SectionColor;
        if (!string.IsNullOrEmpty(config.BackgroundColor)) SettingsManager.Default.Background_colour = config.BackgroundColor;
        if (!string.IsNullOrEmpty(config.AdditionalColor)) SettingsManager.Default.Additional_colour = config.AdditionalColor;
        if (!string.IsNullOrEmpty(config.TextColor)) SettingsManager.Default.Text_colour = config.TextColor;
        if (!string.IsNullOrEmpty(config.ButtonColor)) SettingsManager.Default.Button_colour = config.ButtonColor;
        if (config.BackgroundImage != null) SettingsManager.Default.bgImage = config.BackgroundImage;
        SettingsManager.Save();

        _ = LoadAndApplyBackgroundAsync(config.BackgroundImage);
        ThemeChanged?.Invoke(config);
    }

    private async Task LoadAndApplyBackgroundAsync(string? bgPath)
    {
        if (CurrentTheme == "Custom" && (string.IsNullOrWhiteSpace(bgPath) || bgPath.Equals("none", StringComparison.OrdinalIgnoreCase)))
        {
            CurrentBackgroundBitmap = null;
            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                BackgroundBitmapChanged?.Invoke(null);
            });
            return;
        }

        string targetPath = string.IsNullOrWhiteSpace(bgPath)
            ? "avares://CL.Avalonia/Assets/Backgrounds/DarkThemBG.png"
            : bgPath;

        Bitmap? bmp = null;

        try
        {
            if (targetPath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(targetPath);
                if (AssetLoader.Exists(uri))
                {
                    await using var stream = AssetLoader.Open(uri);
                    bmp = new Bitmap(stream);
                }
            }
            else if (File.Exists(targetPath))
            {
                bmp = await Task.Run(() =>
                {
                    using var fs = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return new Bitmap(fs);
                });
            }

            if (bmp == null)
            {
                bmp = await AvaloniaImageHelper.LoadOptimizedImageAsync(targetPath, 1920);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AvaloniaThemeService] Background load error: {ex.Message}");
        }

        CurrentBackgroundBitmap = bmp;
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            BackgroundBitmapChanged?.Invoke(bmp);
        });
    }

    public void ToggleGlassEffect(bool isGlassDisabled)
    {
        SettingsManager.Default.DisableGlassEffect = isGlassDisabled;
        SettingsManager.Save();

        var resources = Application.Current?.Resources;
        if (resources == null) return;

        Color baseColor = Color.Parse("#FF202020");
        if (resources.TryGetResource("MainBackgroundBrush", null, out var brushObj) && brushObj is SolidColorBrush scb)
        {
            baseColor = scb.Color;
        }

        double opacity = isGlassDisabled ? 1.0 : 0.6;
        resources["GlassTintBrush"] = new SolidColorBrush(baseColor, opacity);
        resources["GlassBlurRadius"] = isGlassDisabled ? 0.0 : 20.0;
        resources["GlassVisibility"] = !isGlassDisabled;
    }

    public async Task<string?> PickColorAsync(string? currentHex)
    {
        Color initialColor = Colors.White;
        if (!string.IsNullOrWhiteSpace(currentHex) && Color.TryParse(NormalizeHex(currentHex), out var parsed))
        {
            initialColor = parsed;
        }

        var dialog = new ColorPickerDialog(initialColor);
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;

        if (owner is { IsVisible: true })
        {
            bool result = await dialog.ShowDialog<bool>(owner);
            if (result)
            {
                var c = dialog.SelectedColor;
                return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            }
        }
        return null;
    }

    public string ExportMainTheme(MainThemeConfig config)
    {
        string json = JsonSerializer.Serialize(config);
        return "CL_THEME|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public void ImportMainTheme(string code)
    {
        try
        {
            if (!code.StartsWith("CL_THEME|"))
            {
                _windowController.ShowSnackbar(
                    "Помилка формату",
                    "Цей код не підходить для Основної Теми.\nЦе точно не для екрану завантаження?");
                return;
            }

            string cleanCode = code.Replace("CL_THEME|", "");
            string json = Encoding.UTF8.GetString(Convert.FromBase64String(cleanCode));

            var config = JsonSerializer.Deserialize<MainThemeConfig>(json);
            if (config != null)
            {
                CurrentTheme = "Custom";
                SettingsManager.Default.Them = "Custom";
                ApplyCustomTheme(config);

                _dialogService.ShowMessage("Тема успішно імпортована!", "Успіх", MascotEmotion.Happy);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Помилка імпорту: {ex.Message}", "Помилка", MascotEmotion.Sad);
        }
    }

    public string ExportLoadScreen(LoadScreenConfig config)
    {
        string json = JsonSerializer.Serialize(config);
        return "CL_LOAD|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public void ImportLoadScreen(string code)
    {
        try
        {
            if (!code.StartsWith("CL_LOAD|"))
            {
                _windowController.ShowSnackbar(
                    "Помилка формату",
                    "Цей код не підходить для Екрану Завантаження.\nМожливо, це код для Основної Теми?");
                return;
            }

            string cleanCode = code.Replace("CL_LOAD|", "");
            string json = Encoding.UTF8.GetString(Convert.FromBase64String(cleanCode));

            var config = JsonSerializer.Deserialize<LoadScreenConfig>(json);
            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.LoadScreenColor))
                    SettingsManager.Default.LoadScreenBarColor = config.LoadScreenColor;
                if (!string.IsNullOrEmpty(config.LoadScreenBg))
                    SettingsManager.Default.LoadScreenBackground = config.LoadScreenBg;
                SettingsManager.Save();

                _dialogService.ShowMessage("Екран завантаження оновлено!\nПерезапустіть лаунчер, щоб побачити зміни.", "Успіх", MascotEmotion.Happy);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"Помилка імпорту LoadScreen: {ex.Message}", "Помилка", MascotEmotion.Sad);
        }
    }

    public void HandleEditPhrasesClick()
    {
        string dataDir = PlatformPaths.DataFolder();
        string phrasesPath = Path.Combine(dataDir, "loading_phrases.txt");

        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

        if (!File.Exists(phrasesPath))
        {
            string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loading_phrases.txt");
            if (File.Exists(legacyPath))
            {
                try { File.Copy(legacyPath, phrasesPath, true); } catch { }
            }
            else
            {
                string rawPhrases = string.Join("\n", new[]
                {
                    "// Кастомні фрази для екрану завантаження (по одній у кожному рядку):",
                    "Осінній дощ тихенько стукає у вікно, а в кімнаті пахне гарячим чаєм...",
                    "Золоте листя кружляє за вікном, немов магічні частинки в грі...",
                    "Homka збирає врожай гарбузів для найзатишнішої ферми у світі...",
                    "Deeplay фотографує туманний світанок та золоті крони осінньої тайги...",
                    "Заварюємо ароматне какао з маршмелоу та загортаємося в теплий плед...",
                    "Данило запалює камін у затишному дерев'яному будиночку...",
                    "WER_Clegendary тестує нові збірки під шум жовтневої зливи...",
                    "кабанчік збирає солодкі ягідки в лісі та пригощає рудих лисичок...",
                    "Осінь на вулиці, тепло в чашці, безмежний затишок на душі..."
                });
                File.WriteAllText(phrasesPath, rawPhrases, Encoding.UTF8);
            }
        }

        try
        {
            UrlOpener.Open(phrasesPath);
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage(
                $"Ой! Не вдалося відкрити файл із фразами.\nТехнічні деталі: {ex.Message}",
                "Халепа з файлом", MascotEmotion.Sad);
        }
    }

    private static string NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return "#FF202020";
        string clean = hex.Trim();
        return clean.StartsWith('#') ? clean : "#" + clean;
    }

    private static bool IsColorLight(string? hexColor)
    {
        if (Color.TryParse(NormalizeHex(hexColor), out var c))
        {
            double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            return luminance > 0.6;
        }
        return false;
    }

    private static void UpdateResource(IResourceDictionary resources, string key, string? hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor)) return;

        try
        {
            if (Color.TryParse(NormalizeHex(hexColor), out var color))
            {
                resources[key] = new SolidColorBrush(color);
            }
        }
        catch
        {
        }
    }
}
