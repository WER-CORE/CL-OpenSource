namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public sealed class GamepadInputGate
{
    public bool IsSuppressed { get; private set; }

    public bool Evaluate(bool isWindowActive, bool isWindowMinimized, bool isGameRunning, GamepadRepeatState repeatState)
    {
        bool allow = isWindowActive && !isWindowMinimized && !isGameRunning;
        if (!allow)
        {
            if (!IsSuppressed)
            {
                repeatState.Reset();
                IsSuppressed = true;
            }
            return false;
        }

        if (IsSuppressed)
        {
            repeatState.Reset();
            IsSuppressed = false;
        }

        return true;
    }
}
