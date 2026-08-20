using System;
using System.Timers;
using CL.Core.Models;

namespace CL.Core.Services
{
    public class GameSessionManager
    {
        private Timer _gameTimer;
        private string _currentMode;

        public event Action<string> OnSessionUpdated;

        public void StartGameSession(string mode)
        {
            _currentMode = mode;

            if (_gameTimer != null) return;

            _gameTimer = new Timer();
            _gameTimer.Interval = TimeSpan.FromMinutes(1).TotalMilliseconds;
            _gameTimer.Elapsed += GameTimer_Tick;
            _gameTimer.Start();
        }

        public void StopGameSession()
        {
            if (_gameTimer != null)
            {
                _gameTimer.Stop();
                _gameTimer.Elapsed -= GameTimer_Tick;
                _gameTimer.Dispose();
                _gameTimer = null;
            }
        }

        private void GameTimer_Tick(object sender, ElapsedEventArgs e)
        {
            double oneMinuteInHours = 1.0 / 60.0;
            string mode = _currentMode?.ToLower();
            if (mode == "vanila" || mode == "vanilla") SettingsManager.Default.StatsGameVanila += oneMinuteInHours;
            else if (mode == "mod") SettingsManager.Default.StatsGameMod += oneMinuteInHours;
            else if (mode == "server") SettingsManager.Default.StatsGameServer += oneMinuteInHours;

            SettingsManager.Save();
            OnSessionUpdated?.Invoke(_currentMode);
        }

        public string GetFormattedStats()
        {
            if (SettingsManager.Default.StatsGameVanila > 0 || SettingsManager.Default.StatsGameMod > 0 || SettingsManager.Default.StatsGameServer > 0)
            {
                var vanila = TimeSpan.FromHours(SettingsManager.Default.StatsGameVanila);
                var mod = TimeSpan.FromHours(SettingsManager.Default.StatsGameMod);
                var server = TimeSpan.FromHours(SettingsManager.Default.StatsGameServer);

                return $"Ванілла: {(int)vanila.TotalHours}h {vanila.Minutes}m\nЗ модами: {(int)mod.TotalHours}h {mod.Minutes}m\nСервери: {(int)server.TotalHours}h {server.Minutes}m";
            }
            else
            {
                return "Немає статистики гри";
            }
        }
    }
}
