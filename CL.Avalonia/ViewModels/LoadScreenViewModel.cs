using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels
{
    public partial class LoadScreenViewModel : ObservableObject
    {
        [ObservableProperty]
        private Bitmap? _backgroundImage;

        [ObservableProperty]
        private IBrush _progressBarBrush = new SolidColorBrush(Color.Parse("#FF00BEFF"));

        [ObservableProperty]
        private string _statusText = "Завантаження ресурсів...";

        [ObservableProperty]
        private double _progressValue = 0;

        [ObservableProperty]
        private string _randomPhraseText = "Привіт!";

        [ObservableProperty]
        private string _versionText = GetFormattedVersion();

        private static string GetFormattedVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var infoVer = System.Linq.Enumerable.FirstOrDefault(
                System.Linq.Enumerable.OfType<System.Reflection.AssemblyInformationalVersionAttribute>(
                    assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                )
            )?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                string cleanVer = infoVer.Split('+')[0];
                return $"v{cleanVer}";
            }

            var ver = assembly.GetName().Version;
            return ver != null ? $"v{ver.ToString(3)}" : "v1.1.0";
        }
        public List<string> RandomPhrases { get; set; } = new();

        public event Action? InitializationCompleted;

        public async Task InitializeAppAsync()
        {

            for (int i = 0; i <= 100; i += 2)
            {
                ProgressValue = i;
                StatusText = $"Завантаження ресурсів... {i}%";
                await Task.Delay(40);
            }

            StatusText = "Ініціалізація завершена!";
            await Task.Delay(300);

            InitializationCompleted?.Invoke();
        }
    }
}
