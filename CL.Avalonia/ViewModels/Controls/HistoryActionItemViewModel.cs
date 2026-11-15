using System;
using System.Collections.Generic;
using CL.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class HistoryActionItemViewModel : ObservableObject
{
    private readonly Action<string, string, int>? _onPlayServer;
    private readonly Action<string, string>? _onPlayOptifine;
    private readonly Action<string>? _onPlayVanilla;

    public string ActionType { get; } = "other";
    public string Name { get; } = "Unknown";
    public string Version { get; } = string.Empty;
    public string Ip { get; } = string.Empty;
    public int Port { get; } = 25565;
    public string Loader { get; } = "vanilla";
    public string LoaderVersion { get; } = string.Empty;

    [ObservableProperty] private string _displayText = string.Empty;

    public HistoryActionItemViewModel()
    {
    }

    public HistoryActionItemViewModel(
        Dictionary<string, string> action,
        Action<string, string, int>? onPlayServer = null,
        Action<string, string>? onPlayOptifine = null,
        Action<string>? onPlayVanilla = null)
    {
        _onPlayServer = onPlayServer;
        _onPlayOptifine = onPlayOptifine;
        _onPlayVanilla = onPlayVanilla;

        ActionType = action.TryGetValue("type", out var t) ? t : "other";
        Name = action.TryGetValue("name", out var n) ? n : "Unknown";
        Version = action.TryGetValue("version", out var v) ? v : string.Empty;
        Ip = action.TryGetValue("ip", out var ip) ? ip : string.Empty;
        if (action.TryGetValue("port", out var pStr) && int.TryParse(pStr, out int p))
        {
            Port = p;
        }
        Loader = action.TryGetValue("loader", out var l) ? l : "vanilla";
        LoaderVersion = action.TryGetValue("loaderVersion", out var lv) ? lv : string.Empty;

        string loaderSuffix = string.IsNullOrEmpty(LoaderVersion) ? Loader : $"{Loader} {LoaderVersion}";

        if (ActionType == "server")
        {
            DisplayText = $"{Name} : {Version} ({loaderSuffix}) : {LocalizationManager.GetString("History.Server", "Сервер")}";
        }
        else if (ActionType == "version")
        {
            DisplayText = $"{Name} : {Version} ({loaderSuffix}) : {LocalizationManager.GetString("History.Version", "Версія")}";
        }
        else
        {
            DisplayText = $"{Name} : {ActionType}";
        }
    }

    [RelayCommand]
    private void ExecuteAction()
    {
        if (ActionType == "server")
        {
            _onPlayServer?.Invoke(Version, Ip, Port);
        }
        else if (ActionType == "version")
        {
            if (Loader.Equals("optifine", StringComparison.OrdinalIgnoreCase))
            {
                _onPlayOptifine?.Invoke(Version, LoaderVersion);
            }
            else
            {
                _onPlayVanilla?.Invoke(Version);
            }
        }
    }
}
