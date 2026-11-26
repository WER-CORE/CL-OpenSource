using System;

namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public interface IGamepadBackend : IDisposable
{
    bool IsInitialized { get; }
    bool HasConnectedDevices { get; }

    bool Initialize();
    void Poll(ref GamepadSnapshot snapshot);
}
