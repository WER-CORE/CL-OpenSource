using System;
using System.Windows;
using System.Windows.Threading;

namespace CL_CLegendary_Launcher_.Class
{
    public class GameSessionManager
    {
        private DispatcherTimer _gameTimer;
        private string _currentMode;

        public void StartGameSession(string mode)
        {
            _currentMode = mode;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_gameTimer != null) return;

                _gameTimer = new DispatcherTimer();
                _gameTimer.Interval = TimeSpan.FromMinutes(1);
                _gameTimer.Tick += GameTimer_Tick;
                _gameTimer.Start();
            });
        }

        public void StopGameSession()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_gameTimer != null)
                {
                    _gameTimer.Stop();
                    _gameTimer.Tick -= GameTimer_Tick;
                    _gameTimer = null;

                    SettingsManager.Save();
                }
            });
        }

        private void GameTimer_Tick(object sender, EventArgs e)
        {
            double oneMinuteInHours = 1.0 / 60.0;

            switch (_currentMode)
            {
                case "vanilla":
                    SettingsManager.Default.StatsGameVanila += oneMinuteInHours;
                    break;
                case "mod":
                    SettingsManager.Default.StatsGameMod += oneMinuteInHours;
                    break;
                case "server":
                    SettingsManager.Default.StatsGameServer += oneMinuteInHours;
                    break;
            }
            SettingsManager.Save();
        }

        public string GetFormattedStats()
        {
            string vanillaTime = FormatTime(SettingsManager.Default.StatsGameVanila);
            string modTime = FormatTime(SettingsManager.Default.StatsGameMod);
            string serverTime = FormatTime(SettingsManager.Default.StatsGameServer);

            string title = LocalizationManager.GetString("GameLaunch.StatsTitle", "Статистика:\n");
            string van = string.Format(LocalizationManager.GetString("GameLaunch.StatsVanilla", "Ваніла: {0};\n"), vanillaTime);
            string mod = string.Format(LocalizationManager.GetString("GameLaunch.StatsModded", "Модові: {0};\n"), modTime);
            string srv = string.Format(LocalizationManager.GetString("GameLaunch.StatsServer", "Сервер: {0}"), serverTime);

            return title + van + mod + srv;
        }

        private string FormatTime(double hours)
        {
            int totalMinutes = (int)Math.Round(hours * 60);

            TimeSpan ts = TimeSpan.FromMinutes(totalMinutes);

            if (ts.TotalHours >= 1)
            {
                if (ts.Minutes > 0)
                    return string.Format(LocalizationManager.GetString("GameLaunch.StatsHoursMins", "{0} год {1} хв"), (int)ts.TotalHours, ts.Minutes);
                else
                    return string.Format(LocalizationManager.GetString("GameLaunch.StatsHours", "{0} год"), (int)ts.TotalHours);
            }
            else
            {
                return string.Format(LocalizationManager.GetString("GameLaunch.StatsMins", "{0} хв"), ts.Minutes);
            }
        }
    }
}