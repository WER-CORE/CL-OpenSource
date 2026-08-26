using CL.Core.Interfaces;
using CL.Core.Platform;
using CL.Core.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public class LastActionService
    {
        private readonly string _lastActionsPath;
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public event Action<List<Dictionary<string, string>>> OnActionsUpdated;

        public LastActionService()
        {
            _lastActionsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "last_actions.json");
        }

        public async Task LoadLastActionsFromJsonAsync()
        {
            try
            {
                if (File.Exists(_lastActionsPath) && SettingsManager.Default.EnableMod_LatestActions)
                {
                    string jsonContent = await File.ReadAllTextAsync(_lastActionsPath);
                    var actions = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(jsonContent);

                    if (actions != null)
                    {
                        OnActionsUpdated?.Invoke(actions);
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                    string.Format(LocalizationManager.GetString("GameLaunch.HistoryLoadErrorDesc", "Не вдалося згадати, що ми робили минулого разу.\nФайл історії, схоже, пошкоджений.\n\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("GameLaunch.HistoryLoadErrorTitle", "Забудькуватість"),
                    MascotEmotion.Sad);
            }
        }

        public async Task AddLastActionAsync(Dictionary<string, string> action)
        {
            await _fileLock.WaitAsync();
            try
            {
                var actions = new List<Dictionary<string, string>>();

                if (File.Exists(_lastActionsPath))
                {
                    var jsonContent = await File.ReadAllTextAsync(_lastActionsPath);
                    actions = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(jsonContent) ?? new List<Dictionary<string, string>>();
                }

                actions.Add(action);

                if (actions.Count > 5)
                {
                    actions = actions.Skip(actions.Count - 5).ToList();
                }

                var updatedJson = JsonConvert.SerializeObject(actions, Formatting.Indented);
                await File.WriteAllTextAsync(_lastActionsPath, updatedJson);

                OnActionsUpdated?.Invoke(actions);
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                    string.Format(LocalizationManager.GetString("GameLaunch.HistorySaveErrorDesc", "Ой! Я намагалася записати цю дію в історію, але щось пішло не так.\n{0}"), ex.Message),
                    LocalizationManager.GetString("GameLaunch.HistorySaveErrorTitle", "Помилка запису"),
                    MascotEmotion.Confused);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}
