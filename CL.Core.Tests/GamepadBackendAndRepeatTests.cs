using System;
using System.Collections.Generic;
using CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;
using Xunit;

namespace CL.Core.Tests;

public class GamepadBackendAndRepeatTests
{
    private sealed class FakeGamepadBackend : IGamepadBackend
    {
        private readonly Dictionary<uint, GamepadSnapshot> _devices = new();
        private uint _activeDeviceId;

        public bool IsInitialized { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool HasConnectedDevices => _devices.Count > 0;
        public uint ActiveDeviceId => _activeDeviceId;

        public bool Initialize()
        {
            if (IsDisposed) return false;
            IsInitialized = true;
            return true;
        }

        public void Connect(uint deviceId, GamepadGlyphStyle glyphStyle = GamepadGlyphStyle.Xbox)
        {
            _devices[deviceId] = new GamepadSnapshot(true, deviceId, GamepadButtonMask.None, 0f, 0f, glyphStyle);
            if (_activeDeviceId == 0)
            {
                _activeDeviceId = deviceId;
            }
        }

        public void SetState(
            uint deviceId,
            GamepadButtonMask buttons = GamepadButtonMask.None,
            float leftStickX = 0f,
            float leftStickY = 0f,
            bool makeActive = true)
        {
            GamepadGlyphStyle style = _devices.TryGetValue(deviceId, out GamepadSnapshot existing)
                ? existing.GlyphStyle
                : GamepadGlyphStyle.Xbox;

            _devices[deviceId] = new GamepadSnapshot(true, deviceId, buttons, leftStickX, leftStickY, style);
            if (makeActive || _activeDeviceId == 0)
            {
                _activeDeviceId = deviceId;
            }
        }

        public void Disconnect(uint deviceId)
        {
            _devices.Remove(deviceId);
            if (_activeDeviceId == deviceId)
            {
                _activeDeviceId = 0;
                foreach (uint remaining in _devices.Keys)
                {
                    _activeDeviceId = remaining;
                    break;
                }
            }
        }

        public void Poll(ref GamepadSnapshot snapshot)
        {
            if (IsDisposed || !IsInitialized || _activeDeviceId == 0 || !_devices.TryGetValue(_activeDeviceId, out GamepadSnapshot active))
            {
                snapshot = GamepadSnapshot.Disconnected;
                return;
            }

            snapshot = active;
        }

        public void Dispose()
        {
            IsDisposed = true;
            IsInitialized = false;
            _devices.Clear();
            _activeDeviceId = 0;
        }
    }

    [Fact]
    public void LeftStick_DeadzoneAndHysteresis_RequireEnterThresholdAndHoldUntilExitThreshold()
    {
        var state = new GamepadRepeatState(enterDeadzone: 0.35f, exitDeadzone: 0.25f);
        Span<UiNavAction> actions = stackalloc UiNavAction[4];

        var belowEnter = new GamepadSnapshot(true, 1, GamepadButtonMask.None, 0.34f, 0f, GamepadGlyphStyle.Xbox);
        Assert.Equal(0, state.Update(belowEnter, 16, actions));

        var enterRight = new GamepadSnapshot(true, 1, GamepadButtonMask.None, 0.36f, 0f, GamepadGlyphStyle.Xbox);
        Assert.Equal(1, state.Update(enterRight, 16, actions));
        Assert.Equal(UiNavAction.Right, actions[0]);

        var inHysteresis = new GamepadSnapshot(true, 1, GamepadButtonMask.None, 0.28f, 0f, GamepadGlyphStyle.Xbox);
        Assert.Equal(0, state.Update(inHysteresis, 16, actions));

        var released = new GamepadSnapshot(true, 1, GamepadButtonMask.None, 0.25f, 0f, GamepadGlyphStyle.Xbox);
        Assert.Equal(0, state.Update(released, 16, actions));
        Assert.Equal(1, state.Update(enterRight, 16, actions));
        Assert.Equal(UiNavAction.Right, actions[0]);

        var up = new GamepadSnapshot(true, 1, GamepadButtonMask.None, 0f, -0.50f, GamepadGlyphStyle.Xbox);
        Assert.Equal(1, state.Update(up, 16, actions));
        Assert.Equal(UiNavAction.Up, actions[0]);
    }

    [Fact]
    public void EdgeButtons_ConfirmBackAndTabs_FireOncePerPressAndRequireRelease()
    {
        var state = new GamepadRepeatState();
        Span<UiNavAction> actions = stackalloc UiNavAction[4];

        var confirmHeld = new GamepadSnapshot(true, 1, GamepadButtonMask.South, 0f, 0f, GamepadGlyphStyle.Nintendo);
        Assert.Equal(1, state.Update(confirmHeld, 16, actions));
        Assert.Equal(UiNavAction.Confirm, actions[0]);

        Assert.Equal(0, state.Update(confirmHeld, 500, actions));
        Assert.Equal(0, state.Update(confirmHeld, 200, actions));

        Assert.Equal(0, state.Update(new GamepadSnapshot(true, 1, GamepadButtonMask.None, 0f, 0f, GamepadGlyphStyle.Nintendo), 16, actions));
        Assert.Equal(1, state.Update(confirmHeld, 16, actions));
        Assert.Equal(UiNavAction.Confirm, actions[0]);

        var backAndTab = new GamepadSnapshot(true, 1, GamepadButtonMask.East | GamepadButtonMask.RightShoulder, 0f, 0f, GamepadGlyphStyle.Xbox);
        int count = state.Update(backAndTab, 16, actions);
        Assert.Equal(2, count);
        Assert.Contains(UiNavAction.Back, actions.Slice(0, count).ToArray());
        Assert.Contains(UiNavAction.TabNext, actions.Slice(0, count).ToArray());
    }

    [Fact]
    public void Directions_HeldPastInitialDelay_RepeatAtConfiguredInterval()
    {
        var state = new GamepadRepeatState(initialRepeatDelayMs: 400, repeatIntervalMs: 120);
        Span<UiNavAction> actions = stackalloc UiNavAction[8];
        var dpadDown = new GamepadSnapshot(true, 1, GamepadButtonMask.DpadDown, 0f, 0f, GamepadGlyphStyle.Xbox);

        Assert.Equal(1, state.Update(dpadDown, 16, actions));
        Assert.Equal(UiNavAction.Down, actions[0]);

        Assert.Equal(0, state.Update(dpadDown, 390, actions));

        Assert.Equal(1, state.Update(dpadDown, 20, actions));
        Assert.Equal(UiNavAction.Down, actions[0]);

        Assert.Equal(1, state.Update(dpadDown, 110, actions));
        Assert.Equal(UiNavAction.Down, actions[0]);
    }

    [Fact]
    public void FakeGamepadBackend_HotPlugDisconnectAndSwitchActiveController_WorksSafely()
    {
        using var backend = new FakeGamepadBackend();
        Assert.True(backend.Initialize());

        backend.Connect(10, GamepadGlyphStyle.Xbox);
        backend.Connect(20, GamepadGlyphStyle.Nintendo);
        Assert.True(backend.HasConnectedDevices);
        Assert.Equal(10u, backend.ActiveDeviceId);

        GamepadSnapshot snapshot = default;
        var state = new GamepadRepeatState();
        Span<UiNavAction> actions = stackalloc UiNavAction[4];

        backend.SetState(10, GamepadButtonMask.South, makeActive: true);
        backend.Poll(ref snapshot);
        Assert.Equal(10u, snapshot.ActiveDeviceId);
        Assert.Equal(1, state.Update(snapshot, 16, actions));
        Assert.Equal(UiNavAction.Confirm, actions[0]);

        backend.Disconnect(10);
        Assert.True(backend.HasConnectedDevices);
        Assert.Equal(20u, backend.ActiveDeviceId);

        backend.SetState(20, GamepadButtonMask.East, makeActive: true);
        backend.Poll(ref snapshot);
        Assert.Equal(20u, snapshot.ActiveDeviceId);
        Assert.Equal(GamepadGlyphStyle.Nintendo, snapshot.GlyphStyle);
        Assert.Equal(0, state.Update(snapshot, 16, actions));
        backend.SetState(20, GamepadButtonMask.None, makeActive: true);
        backend.Poll(ref snapshot);
        Assert.Equal(0, state.Update(snapshot, 16, actions));
        backend.SetState(20, GamepadButtonMask.East, makeActive: true);
        backend.Poll(ref snapshot);
        Assert.Equal(1, state.Update(snapshot, 16, actions));
        Assert.Equal(UiNavAction.Back, actions[0]);

        backend.Disconnect(20);
        Assert.False(backend.HasConnectedDevices);
        backend.Poll(ref snapshot);
        Assert.False(snapshot.IsConnected);
        Assert.Equal(0, state.Update(snapshot, 16, actions));
    }

    [Fact]
    public void Reset_SuppressesAlreadyHeldInputs_UntilPhysicallyReleased()
    {
        var state = new GamepadRepeatState();
        Span<UiNavAction> actions = stackalloc UiNavAction[4];

        var held = new GamepadSnapshot(true, 1, GamepadButtonMask.South | GamepadButtonMask.DpadUp, 0f, 0f, GamepadGlyphStyle.Xbox);
        Assert.Equal(2, state.Update(held, 16, actions));

        state.Reset();

        Assert.Equal(0, state.Update(held, 16, actions));
        Assert.Equal(0, state.Update(held, 500, actions));

        var onlyUpHeld = new GamepadSnapshot(true, 1, GamepadButtonMask.DpadUp, 0f, 0f, GamepadGlyphStyle.Xbox);
        Assert.Equal(0, state.Update(onlyUpHeld, 16, actions));

        var southRepressed = new GamepadSnapshot(true, 1, GamepadButtonMask.South | GamepadButtonMask.DpadUp, 0f, 0f, GamepadGlyphStyle.Xbox);
        Assert.Equal(1, state.Update(southRepressed, 16, actions));
        Assert.Equal(UiNavAction.Confirm, actions[0]);

        Assert.Equal(0, state.Update(new GamepadSnapshot(true, 1, GamepadButtonMask.None, 0f, 0f, GamepadGlyphStyle.Xbox), 16, actions));
        Assert.Equal(1, state.Update(onlyUpHeld, 16, actions));
        Assert.Equal(UiNavAction.Up, actions[0]);
    }

    [Fact]
    public void FakeGamepadBackend_Dispose_CleansStateAndReturnsDisconnected()
    {
        var backend = new FakeGamepadBackend();
        backend.Initialize();
        backend.Connect(1);
        backend.SetState(1, GamepadButtonMask.South, 1f, 0f);

        backend.Dispose();
        backend.Dispose();

        Assert.True(backend.IsDisposed);
        Assert.False(backend.IsInitialized);
        Assert.False(backend.HasConnectedDevices);

        GamepadSnapshot snapshot = new(true, 1, GamepadButtonMask.South, 1f, 1f, GamepadGlyphStyle.Xbox);
        backend.Poll(ref snapshot);
        Assert.False(snapshot.IsConnected);
        Assert.Equal(0u, snapshot.ActiveDeviceId);
    }
}
