using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class GameLogWindow : Window
{
    private readonly ProcessOutputSubscription _processSubscription;
    private readonly PropertyChangedEventHandler _viewModelPropertyChangedHandler;
    private bool _isClosed;

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

        _processSubscription = new ProcessOutputSubscription(ViewModel.AppendLog);
        _viewModelPropertyChangedHandler = OnViewModelPropertyChanged;
        viewModel.PropertyChanged += _viewModelPropertyChangedHandler;
    }

    public void AttachProcess(Process process)
    {
        if (process == null) throw new ArgumentNullException(nameof(process));
        if (_isClosed) return;

        _processSubscription.Attach(process);
    }

    public System.Threading.Tasks.Task LoadLogFromFileAsync(string logFilePath) => ViewModel.LoadLogFromFileAsync(logFilePath);

    public void MinecraftProcess_OutputDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (!_isClosed && !string.IsNullOrEmpty(e.Data))
        {
            ViewModel.AppendLog(e.Data);
        }
    }

    public void MinecraftProcess_ErrorDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (!_isClosed && !string.IsNullOrEmpty(e.Data))
        {
            ViewModel.AppendLog(e.Data);
        }
    }

    public void AppendTextToLog(string text)
    {
        if (!_isClosed)
        {
            ViewModel.AppendLog(text);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isClosed) return;

        if (e.PropertyName == nameof(GameLogViewModel.LogText))
        {
            GameLogTXTMincraft.CaretIndex = GameLogTXTMincraft.Text?.Length ?? 0;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _processSubscription.Dispose();
        ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        ViewModel.Dispose();
        DataContext = null;
        base.OnClosed(e);
    }

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
