namespace CL.Core.Interfaces
{
    public interface ISoundService
    {
        void PlaySound(string resourceName);
        void PlayClick() => PlaySound("click");
        void StopSound();
    }
}
