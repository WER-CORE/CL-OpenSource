namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public readonly struct GamepadSnapshot
{
    public GamepadSnapshot(
        bool isConnected,
        uint activeDeviceId,
        GamepadButtonMask buttons,
        float leftStickX,
        float leftStickY,
        GamepadGlyphStyle glyphStyle)
    {
        IsConnected = isConnected;
        ActiveDeviceId = activeDeviceId;
        Buttons = buttons;
        LeftStickX = leftStickX;
        LeftStickY = leftStickY;
        GlyphStyle = glyphStyle;
    }

    public bool IsConnected { get; }
    public uint ActiveDeviceId { get; }
    public GamepadButtonMask Buttons { get; }
    public float LeftStickX { get; }
    public float LeftStickY { get; }
    public GamepadGlyphStyle GlyphStyle { get; }

    public static GamepadSnapshot Disconnected => default;
}
