using CmlLib.Core.Auth;

namespace CL.Core.Models
{
    public class LaunchConfiguration
    {
        public bool IsOffline { get; set; }
        public int MinimumRamMb { get; set; }
        public int MaximumRamMb { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public MSession Session { get; set; }
        public AccountType AccountType { get; set; }
        public bool IsFullscreen { get; set; }
        public string ServerIp { get; set; }
        public int ServerPort { get; set; }
        public bool JoinServer { get; set; }
    }
}
