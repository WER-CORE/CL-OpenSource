using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class GameLogWindow : Window
{
    public GameLogViewModel ViewModel { get; }

    public GameLogWindow() : this(new GameLogViewModel())
    {
    }

    public GameLogWindow(bool isOfflineMode) : this(new GameLogViewModel(isOfflineMode))
    {
    }

    public GameLogWindow(GameLogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        this.EnableAutoMemoryCleanup();
        DataContext = viewModel;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(GameLogViewModel.LogText))
            {
                GameLogTXTMincraft.CaretIndex = GameLogTXTMincraft.Text?.Length ?? 0;
            }
        };
    }

    public System.Threading.Tasks.Task LoadLogFromFileAsync(string logFilePath) => ViewModel.LoadLogFromFileAsync(logFilePath);

    public void MinecraftProcess_OutputDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            ViewModel.AppendLog(e.Data);
        }
    }

    public void MinecraftProcess_ErrorDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            ViewModel.AppendLog(e.Data);
        }
    }

    public void AppendTextToLog(string text) => ViewModel.AppendLog(text);

    private void BorderTool_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }

    private void MinimizeButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OverlayBackgroundDimmer_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == sender)
        {
            ViewModel.CloseAiOverlayCommand.Execute(null);
        }
    }
}
