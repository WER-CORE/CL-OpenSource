using CL.Core.Interfaces;

namespace CL_CLegendary_Launcher_.Avalonia.Platform
{
    public class NullSoundService : ISoundService
    {
        public void PlaySound(string resourceName) { }
        public void StopSound() { }
    }
}
