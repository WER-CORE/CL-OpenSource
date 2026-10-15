using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CL_CLegendary_Launcher_.Avalonia.ViewModels;
using SukiUI.Controls;
using System;
using System.Collections.Specialized;

namespace CL_CLegendary_Launcher_.Avalonia.Views
{
    public partial class GameLogWindow : SukiWindow
    {
        private GameLogViewModel? _viewModel;
        private bool _scrollPending;

        public GameLogWindow()
        {
            InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_viewModel != null)
                _viewModel.Lines.CollectionChanged -= OnLinesChanged;

            _viewModel = DataContext as GameLogViewModel;

            if (_viewModel != null)
                _viewModel.Lines.CollectionChanged += OnLinesChanged;
        }

        private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || AutoScrollBox.IsChecked != true || _scrollPending)
                return;

            // Прокрутка після розмітки: до неї нові рядки ще не мають контейнерів.
            _scrollPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _scrollPending = false;
                if (_viewModel is { Lines.Count: > 0 } vm)
                    LinesList.ScrollIntoView(vm.Lines.Count - 1);
            }, DispatcherPriority.Background);
        }

        private async void OnCopyClick(object? sender, RoutedEventArgs e)
        {
            if (_viewModel != null && Clipboard != null)
                await Clipboard.SetTextAsync(_viewModel.AllText());
        }
    }
}
