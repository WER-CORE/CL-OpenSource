using System;

namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public sealed class GamepadRepeatState
{
    private readonly float _enterDeadzone;
    private readonly float _exitDeadzone;
    private readonly double _initialRepeatDelayMs;
    private readonly double _repeatIntervalMs;

    private uint _lastActiveDeviceId;
    private GamepadButtonMask _previousButtons;
    private GamepadButtonMask _suppressedButtons;
    private DirectionFlags _stickDirections;
    private DirectionFlags _previousDirections;
    private DirectionFlags _suppressedDirections;
    private bool _requireReleaseAfterReset;

    private UiNavAction _heldDirection;
    private double _holdElapsedMs;
    private bool _initialDelayPassed;

    public GamepadRepeatState(
        float enterDeadzone = 0.35f,
        float exitDeadzone = 0.25f,
        double initialRepeatDelayMs = 400.0,
        double repeatIntervalMs = 120.0)
    {
        _enterDeadzone = enterDeadzone;
        _exitDeadzone = exitDeadzone;
        _initialRepeatDelayMs = initialRepeatDelayMs;
        _repeatIntervalMs = repeatIntervalMs;
    }

    [Flags]
    private enum DirectionFlags : byte
    {
        None = 0,
        Up = 1 << 0,
        Down = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3
    }

    public void Reset()
    {
        _heldDirection = UiNavAction.None;
        _holdElapsedMs = 0;
        _initialDelayPassed = false;
        _requireReleaseAfterReset = true;
    }

    public int Update(in GamepadSnapshot snapshot, double deltaMs, Span<UiNavAction> output)
    {
        if (!snapshot.IsConnected || snapshot.ActiveDeviceId == 0)
        {
            _lastActiveDeviceId = 0;
            _previousButtons = GamepadButtonMask.None;
            _suppressedButtons = GamepadButtonMask.None;
            _stickDirections = DirectionFlags.None;
            _previousDirections = DirectionFlags.None;
            _suppressedDirections = DirectionFlags.None;
            _heldDirection = UiNavAction.None;
            _holdElapsedMs = 0;
            _initialDelayPassed = false;
            _requireReleaseAfterReset = false;
            return 0;
        }

        if (_lastActiveDeviceId != 0 && _lastActiveDeviceId != snapshot.ActiveDeviceId)
        {
            Reset();
        }
        _lastActiveDeviceId = snapshot.ActiveDeviceId;

        UpdateStickHysteresis(snapshot.LeftStickX, snapshot.LeftStickY);
        DirectionFlags currentDirections = CombineDirections(snapshot.Buttons, _stickDirections);

        if (_requireReleaseAfterReset)
        {
            _suppressedButtons = snapshot.Buttons;
            _suppressedDirections = currentDirections;
            _previousButtons = snapshot.Buttons;
            _previousDirections = currentDirections;
            _requireReleaseAfterReset = false;
            return 0;
        }

        _suppressedButtons &= snapshot.Buttons;
        _suppressedDirections &= currentDirections;

        int written = 0;

        written = EmitEdgeAction(snapshot.Buttons, GamepadButtonMask.South, UiNavAction.Confirm, output, written);
        written = EmitEdgeAction(snapshot.Buttons, GamepadButtonMask.East, UiNavAction.Back, output, written);
        written = EmitEdgeAction(snapshot.Buttons, GamepadButtonMask.LeftShoulder, UiNavAction.TabPrev, output, written);
        written = EmitEdgeAction(snapshot.Buttons, GamepadButtonMask.RightShoulder, UiNavAction.TabNext, output, written);

        DirectionFlags effectiveDirections = currentDirections & ~_suppressedDirections;
        UiNavAction activeDirection = PickPrimaryDirection(effectiveDirections);

        if (activeDirection == UiNavAction.None)
        {
            _heldDirection = UiNavAction.None;
            _holdElapsedMs = 0;
            _initialDelayPassed = false;
        }
        else if (activeDirection != _heldDirection)
        {
            _heldDirection = activeDirection;
            _holdElapsedMs = 0;
            _initialDelayPassed = false;
            if (written < output.Length)
            {
                output[written++] = activeDirection;
            }
        }
        else if (deltaMs > 0)
        {
            _holdElapsedMs += deltaMs;
            if (!_initialDelayPassed)
            {
                if (_holdElapsedMs >= _initialRepeatDelayMs)
                {
                    _holdElapsedMs -= _initialRepeatDelayMs;
                    _initialDelayPassed = true;
                    if (written < output.Length)
                    {
                        output[written++] = activeDirection;
                    }
                }
            }

            while (_initialDelayPassed && _holdElapsedMs >= _repeatIntervalMs && written < output.Length)
            {
                _holdElapsedMs -= _repeatIntervalMs;
                output[written++] = activeDirection;
            }
        }

        _previousButtons = snapshot.Buttons;
        _previousDirections = currentDirections;
        return written;
    }

    private int EmitEdgeAction(
        GamepadButtonMask currentButtons,
        GamepadButtonMask flag,
        UiNavAction action,
        Span<UiNavAction> output,
        int written)
    {
        bool isPressed = (currentButtons & flag) != 0;
        bool wasPressed = (_previousButtons & flag) != 0;
        bool isSuppressed = (_suppressedButtons & flag) != 0;

        if (isPressed && !wasPressed && !isSuppressed && written < output.Length)
        {
            output[written++] = action;
        }

        return written;
    }

    private void UpdateStickHysteresis(float x, float y)
    {
        bool left = ApplyAxisHysteresis(-x, (_stickDirections & DirectionFlags.Left) != 0);
        bool right = ApplyAxisHysteresis(x, (_stickDirections & DirectionFlags.Right) != 0);
        bool up = ApplyAxisHysteresis(-y, (_stickDirections & DirectionFlags.Up) != 0);
        bool down = ApplyAxisHysteresis(y, (_stickDirections & DirectionFlags.Down) != 0);

        DirectionFlags next = DirectionFlags.None;
        if (left && !right) next |= DirectionFlags.Left;
        else if (right && !left) next |= DirectionFlags.Right;

        if (up && !down) next |= DirectionFlags.Up;
        else if (down && !up) next |= DirectionFlags.Down;

        _stickDirections = next;
    }

    private bool ApplyAxisHysteresis(float positiveMagnitude, bool wasActive)
    {
        return wasActive
            ? positiveMagnitude > _exitDeadzone
            : positiveMagnitude >= _enterDeadzone;
    }

    private static DirectionFlags CombineDirections(GamepadButtonMask buttons, DirectionFlags stick)
    {
        DirectionFlags dpad = DirectionFlags.None;
        if ((buttons & GamepadButtonMask.DpadUp) != 0) dpad |= DirectionFlags.Up;
        if ((buttons & GamepadButtonMask.DpadDown) != 0) dpad |= DirectionFlags.Down;
        if ((buttons & GamepadButtonMask.DpadLeft) != 0) dpad |= DirectionFlags.Left;
        if ((buttons & GamepadButtonMask.DpadRight) != 0) dpad |= DirectionFlags.Right;

        DirectionFlags combined = dpad | stick;
        if ((combined & (DirectionFlags.Up | DirectionFlags.Down)) == (DirectionFlags.Up | DirectionFlags.Down))
        {
            combined &= ~(DirectionFlags.Up | DirectionFlags.Down);
        }
        if ((combined & (DirectionFlags.Left | DirectionFlags.Right)) == (DirectionFlags.Left | DirectionFlags.Right))
        {
            combined &= ~(DirectionFlags.Left | DirectionFlags.Right);
        }
        return combined;
    }

    private static UiNavAction PickPrimaryDirection(DirectionFlags directions)
    {
        if ((directions & DirectionFlags.Up) != 0) return UiNavAction.Up;
        if ((directions & DirectionFlags.Down) != 0) return UiNavAction.Down;
        if ((directions & DirectionFlags.Left) != 0) return UiNavAction.Left;
        if ((directions & DirectionFlags.Right) != 0) return UiNavAction.Right;
        return UiNavAction.None;
    }
}
