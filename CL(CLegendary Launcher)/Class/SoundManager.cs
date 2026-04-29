using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CL_CLegendary_Launcher_.Class
{
    public static class SoundManager
    {
        private static SoundPlayer _clickPlayer;
        public static void Initialize()
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
            catch (Exception ex)
            {
                MessageBox.Show($"[Audio Init Error]: {ex.Message}");
            }
        }
        public static void Click()
        {
            try
            {
                _clickPlayer?.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"[Audio Play Error]: {ex.Message}");
            }
        }
    }
}
