using CL.Core.Interfaces;
using CL.Core.Services;
using System;
using System.IO;
using System.Media;

namespace CL_CLegendary_Launcher_.PlatformImpl
{
    public class WpfSoundService : ISoundService
    {
        private SoundPlayer _clickPlayer;

        public WpfSoundService()
        {
            try
            {
                Stream audioStream = Resource2.click;
                if (audioStream != null)
                {
                    _clickPlayer = new SoundPlayer(audioStream);
                    _clickPlayer.LoadAsync();
                }
            }
            catch (Exception) { }
        }

        private static readonly object _clickLock = new object();
        private static long _lastClickTimestamp;
        public const int ClickDebounceMs = 150;

        public void PlaySound(string resourceName)
        {
            if (!SettingsManager.Default.EnableSound) return;
            
            if (resourceName == "click" && _clickPlayer != null)
            {
                lock (_clickLock)
                {
                    long now = Environment.TickCount64;
                    if (now - _lastClickTimestamp < ClickDebounceMs) return;
                    _lastClickTimestamp = now;
                }

                try { _clickPlayer.Play(); } catch { }
            }
        }

        public void StopSound()
        {
            try { _clickPlayer?.Stop(); } catch { }
        }
    }
}
