using System;
using System.Collections.Generic;
using System.Diagnostics;
using SDL3;

namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public sealed class Sdl3GamepadBackend : IGamepadBackend
{
    private readonly Dictionary<uint, GamepadEntry> _openGamepads = new();
    private uint _activeDeviceId;
    private bool _ownsSubsystem;
    private bool _disposed;

    private readonly struct GamepadEntry
    {
        public GamepadEntry(IntPtr handle, GamepadGlyphStyle glyphStyle)
        {
            Handle = handle;
            GlyphStyle = glyphStyle;
        }

        public IntPtr Handle { get; }
        public GamepadGlyphStyle GlyphStyle { get; }
    }

    public bool IsInitialized { get; private set; }
    public bool HasConnectedDevices => _openGamepads.Count > 0;
    public string? LastError { get; private set; }

    public bool Initialize()
    {
        if (_disposed) return false;
        if (IsInitialized) return true;

        try
        {
            SDL.SetHint(SDL.Hints.JoystickAllowBackgroundEvents, "1");

            if (!SDL.InitSubSystem(SDL.InitFlags.Gamepad))
            {
                LastError = SDL.GetError();
                Debug.WriteLine($"[Gamepad][SDL3] InitSubSystem(Gamepad) FAILED: {LastError}");
                return false;
            }

            _ownsSubsystem = true;
            IsInitialized = true;
            LastError = null;

            SDL.UpdateGamepads();
            int opened = ScanConnectedGamepads();
            Debug.WriteLine($"[Gamepad][SDL3] Initialized ok=True, initialDevices={opened}");

            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.WriteLine($"[Gamepad][SDL3] Initialize EXCEPTION: {ex}");
            return false;
        }
    }

    public void Poll(ref GamepadSnapshot snapshot)
    {
        if (_disposed || !IsInitialized)
        {
            snapshot = GamepadSnapshot.Disconnected;
            return;
        }

        while (SDL.PollEvent(out SDL.Event ev))
        {
            switch ((SDL.EventType)ev.Type)
            {
                case SDL.EventType.GamepadAdded:
                    TryOpenGamepad(ev.GDevice.Which);
                    break;

                case SDL.EventType.GamepadRemoved:
                    CloseGamepadById(ev.GDevice.Which);
                    break;

                case SDL.EventType.GamepadButtonDown:
                    if (_openGamepads.ContainsKey(ev.GButton.Which))
                    {
                        _activeDeviceId = ev.GButton.Which;
                    }
                    break;

                case SDL.EventType.GamepadAxisMotion:
                    if (Math.Abs((int)ev.GAxis.Value) > 8192 && _openGamepads.ContainsKey(ev.GAxis.Which))
                    {
                        _activeDeviceId = ev.GAxis.Which;
                    }
                    break;
            }
        }

        SDL.UpdateGamepads();

        if (_openGamepads.Count == 0)
        {
            ScanConnectedGamepads();
        }

        if (_activeDeviceId == 0 || !_openGamepads.TryGetValue(_activeDeviceId, out GamepadEntry entry))
        {
            snapshot = GamepadSnapshot.Disconnected;
            return;
        }

        GamepadButtonMask buttons = GamepadButtonMask.None;
        if (SDL.GetGamepadButton(entry.Handle, SDL.GamepadButton.DPadUp)) buttons |= GamepadButtonMask.DpadUp;
        if (SDL.GetGamepadButton(entry.Handle, SDL.GamepadButton.DPadDown)) buttons |= GamepadButtonMask.DpadDown;
        if (SDL.GetGamepadButton(entry.Handle, SDL.GamepadButton.DPadLeft)) buttons |= GamepadButtonMask.DpadLeft;
        if (SDL.GetGamepadButton(entry.Handle, SDL.GamepadButton.DPadRight)) buttons |= GamepadButtonMask.DpadRight;
        if (SDL.GetGamepadButton(entry.Handle, SDL.GamepadButton.South)) buttons |= GamepadButtonMask.South;
        if (SDL.GetGamepadButton(entry.Handle, SDL.GamepadButton.East)) buttons |= GamepadButtonMask.East;
        if (SDL.GetGamepadButton(entry.Handle, SDL.GamepadButton.LeftShoulder)) buttons |= GamepadButtonMask.LeftShoulder;
        if (SDL.GetGamepadButton(entry.Handle, SDL.GamepadButton.RightShoulder)) buttons |= GamepadButtonMask.RightShoulder;

        float leftX = NormalizeAxis(SDL.GetGamepadAxis(entry.Handle, SDL.GamepadAxis.LeftX));
        float leftY = NormalizeAxis(SDL.GetGamepadAxis(entry.Handle, SDL.GamepadAxis.LeftY));

        snapshot = new GamepadSnapshot(
            isConnected: true,
            activeDeviceId: _activeDeviceId,
            buttons: buttons,
            leftStickX: leftX,
            leftStickY: leftY,
            glyphStyle: entry.GlyphStyle);
    }

    private int ScanConnectedGamepads()
    {
        uint[]? ids = SDL.GetGamepads(out _);
        if (ids == null || ids.Length == 0) return 0;

        for (int i = 0; i < ids.Length; i++)
        {
            TryOpenGamepad(ids[i]);
        }
        return _openGamepads.Count;
    }

    private void TryOpenGamepad(uint instanceId)
    {
        if (instanceId == 0 || _openGamepads.ContainsKey(instanceId)) return;

        IntPtr handle = SDL.OpenGamepad(instanceId);
        if (handle == IntPtr.Zero)
        {
            Debug.WriteLine($"[Gamepad][SDL3] OpenGamepad({instanceId}) failed: {SDL.GetError()}");
            return;
        }

        GamepadGlyphStyle glyphStyle = DetectGlyphStyle(handle);
        string? name = SDL.GetGamepadName(handle);
        _openGamepads[instanceId] = new GamepadEntry(handle, glyphStyle);

        if (_activeDeviceId == 0)
        {
            _activeDeviceId = instanceId;
        }

        Debug.WriteLine($"[Gamepad][SDL3] Opened id={instanceId} name='{name}' glyph={glyphStyle} active={_activeDeviceId}");
    }

    private void CloseGamepadById(uint instanceId)
    {
        if (_openGamepads.Remove(instanceId, out GamepadEntry entry) && entry.Handle != IntPtr.Zero)
        {
            SDL.CloseGamepad(entry.Handle);
        }

        if (_activeDeviceId == instanceId)
        {
            _activeDeviceId = 0;
            foreach (uint remainingId in _openGamepads.Keys)
            {
                _activeDeviceId = remainingId;
                break;
            }
        }

        Debug.WriteLine($"[Gamepad][SDL3] Removed id={instanceId} newActive={_activeDeviceId}");
    }

    private static GamepadGlyphStyle DetectGlyphStyle(IntPtr handle)
    {
        return SDL.GetGamepadButtonLabel(handle, SDL.GamepadButton.South) switch
        {
            SDL.GamepadButtonLabel.A => GamepadGlyphStyle.Xbox,
            SDL.GamepadButtonLabel.B => GamepadGlyphStyle.Nintendo,
            SDL.GamepadButtonLabel.Cross => GamepadGlyphStyle.PlayStation,
            _ => GamepadGlyphStyle.Unknown
        };
    }

    private static float NormalizeAxis(short rawValue)
    {
        return Math.Clamp(rawValue / 32767f, -1f, 1f);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (GamepadEntry entry in _openGamepads.Values)
        {
            if (entry.Handle != IntPtr.Zero)
            {
                SDL.CloseGamepad(entry.Handle);
            }
        }
        _openGamepads.Clear();
        _activeDeviceId = 0;

        if (_ownsSubsystem)
        {
            _ownsSubsystem = false;
            IsInitialized = false;
            try
            {
                SDL.QuitSubSystem(SDL.InitFlags.Gamepad);
                SDL.Quit();
                Debug.WriteLine("[Gamepad][SDL3] Disposed and quit SDL3");
            }
            catch
            {
            }
        }
    }
}
