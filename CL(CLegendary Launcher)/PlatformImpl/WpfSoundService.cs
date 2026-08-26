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

        public void PlaySound(string resourceName)
        {
            if (!SettingsManager.Default.EnableSound) return;
            
            if (resourceName == "click" && _clickPlayer != null)
            {
                try { _clickPlayer.Play(); } catch { }
            }
        }

        public void StopSound()
        {
            try { _clickPlayer?.Stop(); } catch { }
        }
    }
}
