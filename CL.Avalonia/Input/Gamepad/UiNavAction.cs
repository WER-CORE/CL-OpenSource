using System;

namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public enum UiNavAction
{
    None = 0,
    Up,
    Down,
    Left,
    Right,
    Confirm,
    Back,
    TabPrev,
    TabNext
}

public enum GamepadGlyphStyle
{
    Unknown = 0,
    Xbox,
    PlayStation,
    Nintendo
}

[Flags]
public enum GamepadButtonMask : ushort
{
    None = 0,
    DpadUp = 1 << 0,
    DpadDown = 1 << 1,
    DpadLeft = 1 << 2,
    DpadRight = 1 << 3,
    South = 1 << 4,
    East = 1 << 5,
    LeftShoulder = 1 << 6,
    RightShoulder = 1 << 7
}
