using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using CL.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using SukiUI;
using SukiUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CL_CLegendary_Launcher_.Avalonia.Platform
{
    public class LauncherTheme
    {
        public LauncherTheme(string key, string fallbackName, ThemeVariant variant, Color primary, Color accent)
        {
            Key = key;
            Name = LocalizationManager.GetString($"Settings.Theme{key}", fallbackName);
            Variant = variant;
            ColorTheme = new SukiColorTheme(key, primary, accent);
        }

        // Значення, яке зберігається в AppSettings.Them, як і у WPF-клієнті.
        public string Key { get; }
        public string Name { get; }
        public ThemeVariant Variant { get; }
        public SukiColorTheme ColorTheme { get; }
        public Uri Background => new Uri($"avares://CL.Avalonia/Assets/Themes/{Key}.png");

        public override string ToString() => Name;
    }

    public partial class ThemeService : ObservableObject
    {
        // Кольори взято з тем WPF-клієнта (Them/*.xaml), фони теж спільні.
        public IReadOnlyList<LauncherTheme> Themes { get; } = new[]
        {
            new LauncherTheme("Dark", "Темна", ThemeVariant.Dark, Color.Parse("#1863C9"), Color.Parse("#4D8FE8")),
            new LauncherTheme("Light", "Світла", ThemeVariant.Light, Color.Parse("#1863C9"), Color.Parse("#4D8FE8")),
            new LauncherTheme("Ocean", "Океан", ThemeVariant.Dark, Color.Parse("#0A8FC4"), Color.Parse("#00D2FF")),
            new LauncherTheme("Forest", "Ліс", ThemeVariant.Dark, Color.Parse("#2E8B22"), Color.Parse("#44D62C")),
            new LauncherTheme("Sunset", "Захід Сонця", ThemeVariant.Dark, Color.Parse("#D35400"), Color.Parse("#FF6A00"))
        };

        [ObservableProperty]
        private LauncherTheme _current;

        [ObservableProperty]
        private Bitmap? _background;

        public ThemeService()
        {
            _current = Find(SettingsManager.Default.Them);
            SukiTheme.GetInstance().AddColorThemes(Themes.Select(t => t.ColorTheme));
        }

        public void Apply()
        {
            var app = Application.Current;
            if (app == null)
                return;

            app.RequestedThemeVariant = Current.Variant;
            SukiTheme.GetInstance().ChangeColorTheme(Current.ColorTheme);

            using var stream = AssetLoader.Open(Current.Background);
            Background = new Bitmap(stream);
        }

        partial void OnCurrentChanged(LauncherTheme value)
        {
            SettingsManager.Default.Them = value.Key;
            SettingsManager.Save();
            Apply();
        }

        // Власна тема ("Custom") тут ще не підтримується, тож вона, як і невідомі значення, відкривається темною.
        private LauncherTheme Find(string? key) => Themes.FirstOrDefault(t => t.Key == key) ?? Themes[0];
    }
}
