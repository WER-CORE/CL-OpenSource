using Avalonia.Threading;
using CL.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels
{
    public partial class GameLogViewModel : ObservableObject
    {
        private const int MaxLines = 20000;

        // Гра пише тисячі рядків за секунду; рядки збираються в чергу і
        // переносяться в список одним проходом, а не окремим Post на кожен.
        private readonly ConcurrentQueue<string> _pending = new ConcurrentQueue<string>();
        private int _flushScheduled;

        public ObservableCollection<string> Lines { get; } = new ObservableCollection<string>();

        public void Attach(Process process) => GameLogReader.StartWithOutput(process, Append);

        public async Task LoadFromFileAsync(string logFilePath)
            => Append(await GameLogReader.ReadCrashLogAsync(logFilePath));

        public string AllText() => string.Join(Environment.NewLine, Lines);

        [RelayCommand]
        private void OpenLogsFolder()
        {
            string folder = Path.Combine(SettingsManager.Default.PathLacunher, "logs");
            if (Directory.Exists(folder))
                UrlOpener.Open(folder);
        }

        [RelayCommand]
        private void Clear() => Lines.Clear();

        private void Append(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (var line in text.Split('\n'))
                _pending.Enqueue(line.TrimEnd('\r'));

            if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
                Dispatcher.UIThread.Post(Flush, DispatcherPriority.Background);
        }

        private void Flush()
        {
            Interlocked.Exchange(ref _flushScheduled, 0);

            while (_pending.TryDequeue(out var line))
                Lines.Add(line);

            int overflow = Lines.Count - MaxLines;
            for (int i = 0; i < overflow; i++)
                Lines.RemoveAt(0);
        }
    }
}
