using CL_CLegendary_Launcher_.Windows;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WpfAnimatedGif;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;

namespace CL_CLegendary_Launcher_.Class
{
    public class MainThemeConfig
    {
        public string SectionColor { get; set; }
        public string BackgroundColor { get; set; }
        public string AdditionalColor { get; set; }
        public string TextColor { get; set; }
        public string ButtonColor { get; set; }
        public string BackgroundImage { get; set; }
    }

    public class LoadScreenConfig
    {
        public string LoadScreenColor { get; set; }
        public string LoadScreenBg { get; set; }
    }
    public class ThemeService
    {
        private readonly CL_Main_ _main;
        public static string currentTheme = "Dark";
        private readonly string _customThemePath;
        public event Action<string> OnThemeChanged;

        public ThemeService(CL_Main_ main)
        {
            _main = main;
            _customThemePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "CustomThem.xaml");
        }

        public void InitializeTheme()
        {
            EnsureCustomThemeFileExists();
            ApplyTheme(SettingsManager.Default.Them);
            LoadBackgroundImage();
            SetColourButtons();
        }

        public void ApplyTheme(string theme)
        {
            ApplicationTheme targetSystemTheme = theme == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark;

            ApplicationThemeManager.Apply(targetSystemTheme, WindowBackdropType.Mica, true);

            ImageBehavior.SetAnimatedSource(_main.Bg, null);

            var dictionariesToAdd = new List<ResourceDictionary>();

            try
            {
                switch (theme)
                {
                    case "Dark":
                        SettingsManager.Default.bgImage = "";
                        SettingsManager.Save();

                        dictionariesToAdd.Add(new ResourceDictionary { Source = new Uri("Them/DarkTheme.xaml", UriKind.Relative) });
                        dictionariesToAdd.Add(new ResourceDictionary { Source = new Uri("Them/DarkThemeSerLisAndUpdMinecraft.xaml", UriKind.Relative) });

                        _main.Bg.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/DarkThemBG.webp"));
                        break;

                    case "Light":
                        SettingsManager.Default.bgImage = "";
                        SettingsManager.Save();

                        dictionariesToAdd.Add(new ResourceDictionary { Source = new Uri("Them/LightTheme.xaml", UriKind.Relative) });
                        dictionariesToAdd.Add(new ResourceDictionary { Source = new Uri("Them/LightThemeSerLisAndUpdMinecraft.xaml", UriKind.Relative) });

                        _main.Bg.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/LightThemBG.webp"));
                        break;

                    case "Custom":
                        dictionariesToAdd.Add(new ResourceDictionary { Source = new Uri(_customThemePath, UriKind.Absolute) });
                        break;

                    default:
                        dictionariesToAdd.Add(new ResourceDictionary { Source = new Uri("Them/DarkTheme.xaml", UriKind.Relative) });
                        break;
                }

                var mergedDicts = Application.Current.Resources.MergedDictionaries;
                var dictsToRemove = new List<ResourceDictionary>();
                foreach (var dict in mergedDicts)
                {
                    if (dict.Source != null)
                    {
                        string source = dict.Source.ToString();
                        if (source.Contains("Them/") || source.Contains("CustomThem.xaml"))
                        {
                            dictsToRemove.Add(dict);
                        }
                    }
                }

                foreach (var dict in dictsToRemove)
                {
                    mergedDicts.Remove(dict);
                }

                foreach (var dict in dictionariesToAdd)
                {
                    mergedDicts.Add(dict);
                }

                bool glassDisabled = SettingsManager.Default.DisableGlassEffect;
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is Wpf.Ui.Controls.FluentWindow fluentWindow)
                    {
                        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(fluentWindow);

                        if (!glassDisabled)
                        {
                            fluentWindow.WindowBackdropType = WindowBackdropType.Mica;
                            fluentWindow.Background = Brushes.Transparent;
                        }
                        else
                        {
                            fluentWindow.WindowBackdropType = WindowBackdropType.None;
                            fluentWindow.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "MainBackgroundBrush");
                        }
                    }
                    else
                    {
                        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(window);
                    }
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(string.Format(LocalizationManager.GetString("ThemesCustomization.ThemeError", "Помилка теми: {0}"), ex.Message));
                return;
            }

            currentTheme = theme;
            UpdateGlassEffect();
        }

        public void HandleResetCustomThemeClick()
        {
            EnsureCustomThemeFileExists();
            _main.Click();

            SettingsManager.Default.bgImage = "";

            SettingsManager.Default.Button_colour = "#FF202020";
            SettingsManager.Default.Section_colour = "#303030";
            SettingsManager.Default.Background_colour = "#FF202020";
            SettingsManager.Default.Additional_colour = "#FF1863C9";
            SettingsManager.Default.Text_colour = "#FFFFFF";
            SettingsManager.Default.Them = "Dark";
            SettingsManager.Save();

            UpdateColorForElement(_customThemePath, "MainBackgroundBrushServer", "#303030");
            UpdateColorForElement(_customThemePath, "MainBackgroundBrush", "#FF202020");
            UpdateColorForElement(_customThemePath, "MainBackgroundProgressBar", "#FF1863C9");
            UpdateColorForElement(_customThemePath, "MainForegroundBrush", "#FFFFFF");
            UpdateColorForElement(_customThemePath, "MainBackgroundButton", "#FF202020");

            ApplyTheme("Dark");
            SetColourButtons();
        }
        public object CreateColorButtonContent(string colorHex)
        {
            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(colorHex);

                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                var colorSwatch = new Border
                {
                    Width = 16,
                    Height = 16,
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = new SolidColorBrush(color),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(100, 128, 128, 128))
                };

                var textBlock = new Wpf.Ui.Controls.TextBlock
                {
                    Text = colorHex,
                    VerticalAlignment = VerticalAlignment.Center
                };

                panel.Children.Add(colorSwatch);
                panel.Children.Add(textBlock);

                return panel;
            }
            catch
            {
                return colorHex;
            }
        }

        private void UpdateGlassEffect()
        {
            try
            {
                bool isDisabled = SettingsManager.Default.DisableGlassEffect;
                Application.Current.Resources["GlassBlurRadius"] = isDisabled ? 0.0 : 20.0;
                Application.Current.Resources["GlassVisibility"] = isDisabled ? Visibility.Collapsed : Visibility.Visible;

                var baseColorObj = Application.Current.TryFindResource("MainBackgroundBrush");
                Color baseColor = Colors.Black;

                if (baseColorObj is Color c) baseColor = c;
                else if (baseColorObj is SolidColorBrush scb) baseColor = scb.Color;

                double targetOpacity = isDisabled ? 1.0 : 0.6;

                var tintBrush = new SolidColorBrush(baseColor) { Opacity = targetOpacity };
                if (tintBrush.CanFreeze) tintBrush.Freeze();

                Application.Current.Resources["GlassTintBrush"] = tintBrush;
            }
            catch (Exception) { }
        }

        public void ToggleGlassEffect(bool shouldDisable)
        {
            SettingsManager.Default.DisableGlassEffect = shouldDisable;
            SettingsManager.Save();

            ApplyTheme(currentTheme);
            LoadBackgroundImage();
        }
        public void EnsureCustomThemeFileExists()
        {
            string directoryPath = Path.GetDirectoryName(_customThemePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (!File.Exists(_customThemePath))
            {
                string defaultXaml = @"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                                                      xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <SolidColorBrush x:Key=""MainBackgroundButton"">#FF202020</SolidColorBrush>
    <SolidColorBrush x:Key=""MainForegroundBrush"">#FFFFFFFF</SolidColorBrush>
    <SolidColorBrush x:Key=""MainBackgroundProgressBar"">#FF1863C9</SolidColorBrush>
    <SolidColorBrush x:Key=""MainBackgroundBrushServer"">#FF303030</SolidColorBrush>
    <SolidColorBrush x:Key=""MainBorderBrush"">#FFFFFFFF</SolidColorBrush>
    <SolidColorBrush x:Key=""MainBackgroundBrush"">#FF202020</SolidColorBrush>
</ResourceDictionary>";

                File.WriteAllText(_customThemePath, defaultXaml);
            }
        }
        public void LoadBackgroundImage()
        {
            string bg = SettingsManager.Default.bgImage;
            if (string.IsNullOrWhiteSpace(bg) || !File.Exists(bg))
            {
                SettingsManager.Default.bgImage = "";
                return;
            }

            try
            {
                Uri uri = new(bg, UriKind.Absolute);
                ImageBehavior.SetAnimatedSource(_main.Bg, null);

                if (bg.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    var gifImage = new BitmapImage(uri);
                    ImageBehavior.SetAnimatedSource(_main.Bg, gifImage);
                }
                else
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.UriSource = uri;
                    img.DecodePixelWidth = 1080;
                    img.DecodePixelHeight = 1920;
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                    _main.Bg.Source = img;
                }
                _main.Bg.Opacity = 0.75;
            }
            catch
            {
                SettingsManager.Default.bgImage = "";
                ApplyTheme(currentTheme);
            }
        }

        public void SetColourButtons()
        {
            _main.Section_colourButton.Content = CreateColorButtonContent(SettingsManager.Default.Section_colour);
            _main.Background_colourButton.Content = CreateColorButtonContent(SettingsManager.Default.Background_colour);
            _main.Additional_colourButton.Content = CreateColorButtonContent(SettingsManager.Default.Additional_colour);
            _main.Text_colourButton.Content = CreateColorButtonContent(SettingsManager.Default.Text_colour);
            _main.Button_colourButton.Content = CreateColorButtonContent(SettingsManager.Default.Button_colour);
        }
        public void HandleBackgroundImageClick()
        {
            if (SettingsManager.Default.Them != "Custom")
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("ThemesCustomization.CustomThemeRequiredDesc", "Змінювати фон можна тільки коли обрана тема 'Кастомна'!\nБудь ласка, перемкни тему у списку вище, щоб поставити свою картинку."),
                    LocalizationManager.GetString("ThemesCustomization.CustomThemeRequiredTitle", "Обмеження"),
                    MascotEmotion.Confused
                );
                return;
            }

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.gif)|*.png;*.jpg;*.gif",
                Title = LocalizationManager.GetString("ThemesCustomization.SelectBackgroundTitle", "Виберіть фон")
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SettingsManager.Default.bgImage = openFileDialog.FileName;
                SettingsManager.Save();
                LoadBackgroundImage();
            }
        }
        public void HandleColorChange(Button button, string settingKey, string resourceKey)
        {
            try
            {
                var colorHex = ShowColorDialog();
                if (!string.IsNullOrEmpty(colorHex))
                {
                    button.Content = CreateColorButtonContent(colorHex);

                    UpdateColorForElement(_customThemePath, resourceKey, colorHex);

                    SettingsManager.Default[settingKey] = colorHex;
                    SettingsManager.Save();

                    if (currentTheme == "Custom")
                    {
                        ApplyTheme("Custom");
                    }
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("ThemesCustomization.ColorChangeErrorDesc", "Не змогла змінити колір.\n{0}"), ex.Message),
                    LocalizationManager.GetString("ThemesCustomization.ColorChangeErrorTitle", "Помилка"),
                    MascotEmotion.Sad);
            }
        }

        public void HandleSaveCustomThemeClick()
        {
            currentTheme = "Custom";
            ApplyTheme("Custom");
            SettingsManager.Default.Them = "Custom";
            SettingsManager.Save();
        }
        private string ShowColorDialog()
        {
            using (var colorDialog = new System.Windows.Forms.ColorDialog())
            {
                colorDialog.FullOpen = true;
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return $"#{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}";
                }
            }
            return null;
        }

        private void UpdateColorForElement(string resourceFilePath, string key, string colorHex)
        {
            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(colorHex);
                var resourceDictionary = LoadResourceDictionary(resourceFilePath);
                if (resourceDictionary != null)
                {
                    resourceDictionary[key] = new SolidColorBrush(color);
                    SaveResourceDictionary(resourceDictionary, resourceFilePath);
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("ThemesCustomization.ColorUpdateErrorDesc", "Ой, щось не так з кольором.\n{0}"), ex.Message),
                    LocalizationManager.GetString("ThemesCustomization.ColorUpdateErrorTitle", "Збій"),
                    MascotEmotion.Confused);
            }
        }

        private void SaveResourceDictionary(ResourceDictionary resourceDictionary, string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    System.Windows.Markup.XamlWriter.Save(resourceDictionary, stream);
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("ThemesCustomization.ThemeSaveErrorDesc", "Не вдалося зберегти налаштування теми.\n{0}"), ex.Message),
                    LocalizationManager.GetString("ThemesCustomization.ThemeSaveErrorTitle", "Помилка"),
                    MascotEmotion.Sad);
            }
        }

        private ResourceDictionary LoadResourceDictionary(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    return (ResourceDictionary)System.Windows.Markup.XamlReader.Load(stream);
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("ThemeSaveErrorTitle.ThemeLoadErrorDesc", "Не можу прочитати файл теми.\n{0}"), ex.Message),
                    LocalizationManager.GetString("ThemeSaveErrorTitle.ThemeLoadErrorTitle", "Помилка"),
                    MascotEmotion.Sad);
                return null;
            }
        }
        public void HandleLoadScreenBackgroundClick()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                SettingsManager.Default.LoadScreenBackground = openFileDialog.FileName;
                SettingsManager.Save();
                NotificationService.ShowNotification(
                            LocalizationManager.GetString("ThemeLoadErrorTitle.LoadScreenBgUpdatedDesc", "Вау! Тепер завантаження виглядатиме стильно.\nФон успішно оновлено!"),
                            LocalizationManager.GetString("ThemeLoadErrorTitle.LoadScreenBgUpdatedTitle", "Новий стиль"),
                            _main.SnackbarPresenter,
                            default,
                            default,
                            Wpf.Ui.Controls.ControlAppearance.Info
                        );
            }
        }
        public void HandleEditPhrasesClick()
        {
            string phrasesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loading_phrases.txt");
            string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            if (!File.Exists(phrasesPath))
            {
                string rawPhrases = LocalizationManager.GetString("ThemesCustomization.DefaultLoadingPhrases", "Перша зелена травичка пробивається крізь землю...\nЗаварюємо свіжий фруктовий чай...\nHomka саджає перші весняні квіти...\nDeeplay фотографує цвітіння сакур на камеру 🌸...\nТеплий весняний дощик стукає по вікнах...\nЧас ховати зимові куртки далеко в шафу...\nПахне свіжоскошеною травою та бузком...\nДерева вкриваються ніжним білим цвітом...\nДанило готує мангал для перших шашликів...\nWER_Clegendary шукає ідеальне місце для пікніка...\nПрирода нарешті прокидається за вікном...\nМружимося від яскравого весняного сонечка...\nПелюстки вишень кружляють у теплому повітрі...\nЧас виходити на довгі вечірні прогулянки...\nСвіже весняне повітря надихає на пригоди...\nГотуємось до теплих травневих вихідних...\nВсі чекають на потепління (або вже садять картоплю)...\nТепло на вулиці, сонячно на душі...");

                var defaultPhrases = new List<string>(rawPhrases.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries));

                File.WriteAllLines(phrasesPath, defaultPhrases);
            }

            try
            {
                Process.Start(new ProcessStartInfo(phrasesPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                            string.Format(LocalizationManager.GetString("ThemesCustomization.PhrasesFileOpenErrorDesc", "Ой! Не вдалося відкрити файл із фразами.\nМожливо, у тебе немає Блокнота або файл хтось тримає?\n\nТехнічні деталі: {0}"), ex.Message),
                            LocalizationManager.GetString("ThemesCustomization.PhrasesFileOpenErrorTitle", "Халепа з файлом"),
                            MascotEmotion.Sad
                        );
            }
        }
        public void HandleLoadScreenColorClick(Button previewButton)
        {
            ColorDialog colorDialog = new ColorDialog();
            colorDialog.FullOpen = true;

            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                var c = colorDialog.Color;
                string hexColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";

                SettingsManager.Default.LoadScreenBarColor = hexColor;
                SettingsManager.Save();

                if (previewButton != null)
                {
                    previewButton.Content = CreateColorButtonContent(hexColor);
                }
            }
        }
        public void HandleResetLoadScreenClick(Button previewButton)
        {
            var result = MascotMessageBox.Ask(
                LocalizationManager.GetString("ThemesCustomization.ResetLoadScreenDesc", "Хочеш повернути все як було?\nЦе скине твій фон та колір смужки на стандартні налаштування."),
                LocalizationManager.GetString("ThemesCustomization.ResetLoadScreenTitle", "Генеральне прибирання"),
                MascotEmotion.Confused
            );

            if (result == true)
            {
                SettingsManager.Default.LoadScreenBackground = "";
                SettingsManager.Default.LoadScreenBarColor = "";
                SettingsManager.Save();

                if (previewButton != null)
                {
                    string defaultColor = "#00BEFF";
                    previewButton.Content = CreateColorButtonContent(defaultColor);
                }

                NotificationService.ShowNotification(
                    LocalizationManager.GetString("ThemesCustomization.ResetLoadScreenSuccessTitle", "Готово!"),
                    LocalizationManager.GetString("ThemesCustomization.ResetLoadScreenSuccessDesc", "Екран завантаження тепер чистий, як перший сніг."),
                    _main.SnackbarPresenter
                );
            }
        }
        public string ExportMainTheme()
        {
            var config = new MainThemeConfig
            {
                SectionColor = SettingsManager.Default.Section_colour,
                BackgroundColor = SettingsManager.Default.Background_colour,
                AdditionalColor = SettingsManager.Default.Additional_colour,
                TextColor = SettingsManager.Default.Text_colour,
                ButtonColor = SettingsManager.Default.Button_colour,
                BackgroundImage = SettingsManager.Default.bgImage
            };
            string json = JsonConvert.SerializeObject(config);

            return "CL_THEME|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        public void ImportMainTheme(string code)
        {
            try
            {
                if (!code.StartsWith("CL_THEME|"))
                {
                    NotificationService.ShowNotification(
                        LocalizationManager.GetString("ThemesCustomization.ImportMainThemeFormatErrorDesc", "Цей код не підходить для Основної Теми.\nЦе точно не для екрану завантаження?"),
                        LocalizationManager.GetString("ThemesCustomization.ImportMainThemeFormatErrorTitle", "Помилка формату"),
                        _main.SnackbarPresenter, default, default, Wpf.Ui.Controls.ControlAppearance.Danger);
                    return;
                }

                string cleanCode = code.Replace("CL_THEME|", "");
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(cleanCode));
                var config = JsonConvert.DeserializeObject<MainThemeConfig>(json);

                SettingsManager.Default.Section_colour = config.SectionColor;
                SettingsManager.Default.Background_colour = config.BackgroundColor;
                SettingsManager.Default.Additional_colour = config.AdditionalColor;
                SettingsManager.Default.Text_colour = config.TextColor;
                SettingsManager.Default.Button_colour = config.ButtonColor;
                SettingsManager.Default.bgImage = config.BackgroundImage;

                SettingsManager.Default.Them = "Custom";
                SettingsManager.Save();

                GenerateAndSaveCustomXaml(config);

                LoadBackgroundImage();

                ApplyTheme("Custom");

                SetColourButtons();

                MascotMessageBox.Show(
                    LocalizationManager.GetString("ThemesCustomization.ImportThemeSuccessDesc", "Тема успішно імпортована!"),
                    LocalizationManager.GetString("ThemesCustomization.ImportThemeSuccessTitle", "Успіх"),
                    MascotEmotion.Happy);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("ThemesCustomization.ImportErrorDesc", "Помилка імпорту: {0}"), ex.Message),
                    LocalizationManager.GetString("ThemesCustomization.ImportErrorTitle", "Помилка"),
                    MascotEmotion.Sad);
            }
        }
        private void GenerateAndSaveCustomXaml(MainThemeConfig config)
        {
            EnsureCustomThemeFileExists();

            string newXamlContent = $@"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                                      xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <SolidColorBrush x:Key=""MainBackgroundButton"">{config.ButtonColor}</SolidColorBrush>
    <SolidColorBrush x:Key=""MainForegroundBrush"">{config.TextColor}</SolidColorBrush>
    <SolidColorBrush x:Key=""MainBackgroundProgressBar"">{config.AdditionalColor}</SolidColorBrush>
    <SolidColorBrush x:Key=""MainBackgroundBrushServer"">{config.SectionColor}</SolidColorBrush>
    <SolidColorBrush x:Key=""MainBorderBrush"">{config.TextColor}</SolidColorBrush> 
    <SolidColorBrush x:Key=""MainBackgroundBrush"">{config.BackgroundColor}</SolidColorBrush>
</ResourceDictionary>";

            try
            {
                File.WriteAllText(_customThemePath, newXamlContent);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(string.Format(LocalizationManager.GetString("ImportThemeSuccessDesc.CustomXamlSaveError", "Не вдалося зберегти файл теми:\n{0}"), ex.Message));
            }
        }
        public string ExportLoadScreen()
        {
            var config = new LoadScreenConfig
            {
                LoadScreenColor = SettingsManager.Default.LoadScreenBarColor,
                LoadScreenBg = SettingsManager.Default.LoadScreenBackground
            };
            string json = JsonConvert.SerializeObject(config);
            return "CL_LOAD|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        public void ImportLoadScreen(string code)
        {
            try
            {
                if (!code.StartsWith("CL_LOAD|"))
                {
                    NotificationService.ShowNotification(
                        LocalizationManager.GetString("ThemesCustomization.ImportLoadScreenFormatErrorDesc", "Цей код не підходить для Екрану Завантаження.\nМожливо, це код для Основної Теми?"),
                        LocalizationManager.GetString("ThemesCustomization.ImportLoadScreenFormatErrorTitle", "Помилка формату"),
                        _main.SnackbarPresenter, default, default, Wpf.Ui.Controls.ControlAppearance.Danger);
                    return;
                }

                string cleanCode = code.Replace("CL_LOAD|", "");
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(cleanCode));
                var config = JsonConvert.DeserializeObject<LoadScreenConfig>(json);

                SettingsManager.Default.LoadScreenBarColor = config.LoadScreenColor;
                SettingsManager.Default.LoadScreenBackground = config.LoadScreenBg;
                SettingsManager.Save();

                MascotMessageBox.Show(
                    LocalizationManager.GetString("ThemesCustomization.ImportLoadScreenSuccessDesc", "Екран завантаження оновлено!\nПерезапустіть лаунчер, щоб побачити зміни."),
                    LocalizationManager.GetString("ThemesCustomization.ImportLoadScreenSuccessTitle", "Успіх"),
                    MascotEmotion.Happy);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(string.Format(LocalizationManager.GetString("ThemesCustomization.ImportLoadScreenError", "Помилка імпорту LoadScreen: {0}"), ex.Message));
            }
        }
    }
}