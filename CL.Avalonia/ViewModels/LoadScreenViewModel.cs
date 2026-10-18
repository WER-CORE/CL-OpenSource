using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels
{
    public partial class LoadScreenViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _statusText = "Завантаження ресурсів...";

        [ObservableProperty]
        private double _progressValue = 0;

        [ObservableProperty]
        private string _randomPhraseText = "Привіт!";
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
