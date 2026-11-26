using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CL.Core.Services;

namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public sealed class GamepadInputService : IDisposable
{
    private static readonly TimeSpan ActivePollInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromMilliseconds(250);

    private readonly IGamepadBackend _backend;
    private readonly GamepadNavigationRouter _router;
    private readonly GamepadRepeatState _repeatState = new();
    private readonly GamepadInputGate _gate = new();
    private readonly UiNavAction[] _actionBuffer = new UiNavAction[8];
    private readonly Stopwatch _stopwatch = new();

    private GamepadSnapshot _snapshot;
    private DispatcherTimer? _timer;
    private bool _isStarted;
    private bool _disposed;
    private bool? _lastGateAllowed;

    public GamepadInputService(IGamepadBackend backend, GamepadNavigationRouter router)
    {
        _backend = backend;
        _router = router;
    }

    public bool IsRunning => _isStarted && !_disposed;

    public void Start()
    {
        if (_disposed || _isStarted) return;

        bool ok = _backend.Initialize();
        Debug.WriteLine($"[Gamepad][Input] Start: backendInit={ok} hasDevices={_backend.HasConnectedDevices}");
        if (!ok) return;

        _isStarted = true;
        _stopwatch.Restart();

        _timer = new DispatcherTimer(IdlePollInterval, DispatcherPriority.Input, OnTick);
        _timer.Start();
    }

    public void Stop()
    {
        if (!_isStarted) return;
        _isStarted = false;

        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }

        _repeatState.Reset();
        _lastGateAllowed = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed || !_isStarted) return;

        double deltaMs = _stopwatch.Elapsed.TotalMilliseconds;
        _stopwatch.Restart();

        Window? activeWindow = ResolveActiveWindow();
        bool isWindowActive = activeWindow is { IsActive: true, IsVisible: true };
        bool isMinimized = activeWindow?.WindowState == WindowState.Minimized;
        bool isGameRunning = IsGameProcessRunning();

        bool allowed = _gate.Evaluate(isWindowActive, isMinimized, isGameRunning, _repeatState);
        if (_lastGateAllowed != allowed)
        {
            _lastGateAllowed = allowed;
            Debug.WriteLine($"[Gamepad][Input] Gate allowed={allowed} (active={isWindowActive}, minimized={isMinimized}, gameRunning={isGameRunning}, win={activeWindow?.GetType().Name ?? "null"})");
        }

        if (!allowed)
        {
            SetTimerInterval(IdlePollInterval);
            return;
        }

        _backend.Poll(ref _snapshot);
        SetTimerInterval(_backend.HasConnectedDevices ? ActivePollInterval : IdlePollInterval);

        int count = _repeatState.Update(in _snapshot, deltaMs, _actionBuffer);
        for (int i = 0; i < count; i++)
        {
            UiNavAction action = _actionBuffer[i];
            Debug.WriteLine($"[Gamepad][Input] Dispatch action={action} device={_snapshot.ActiveDeviceId}");
            _router.HandleAction(action, activeWindow);
        }
    }

    private void SetTimerInterval(TimeSpan interval)
    {
        if (_timer != null && _timer.Interval != interval)
        {
            _timer.Interval = interval;
        }
    }

    private static Window? ResolveActiveWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var windows = desktop.Windows;
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            Window w = windows[i];
            if (w.IsVisible && w.IsActive)
            {
                return w;
            }
        }

        return desktop.MainWindow;
    }

    private static bool IsGameProcessRunning()
    {
        try
        {
            Process? proc = GameLaunchService.ActiveProcess;
            return proc != null && !proc.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _backend.Dispose();
    }
}
