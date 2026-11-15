using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Views;
using CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public sealed class FirstRunCoordinator
{
    private readonly SemaphoreSlim _runGate = new SemaphoreSlim(1, 1);
    private bool _hasCompletedSequence;

    public async Task RunAsync(CL_Main mainWindow)
    {
        if (mainWindow == null || _hasCompletedSequence)
            return;

        if (!await _runGate.WaitAsync(0))
            return;

        try
        {
            if (_hasCompletedSequence)
                return;

            await Task.Delay(250);

            if (!SettingsManager.Default.WpfImportPromptHandled)
            {
                string? candidate = await Task.Run(() => LegacyMigrationService.FindLegacyWpfDataCandidate());
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    var dialog = new WpfImportDialog(candidate);
                    await dialog.ShowDialog(mainWindow);

                    if (dialog.WasImported)
                    {
                        await mainWindow.RefreshAfterLegacyImportAsync();
                    }
                    else if (dialog.WasDeclined)
                    {
                        AvaloniaDialogService.Show(
                            LocalizationManager.GetString(
                                "Settings.ImportLegacyLaterHint",
                                "Ви можете імпортувати старі дані пізніше у розділі Файли та бекапи → Імпорт Data."),
                            LocalizationManager.GetString("Settings.ImportLegacyTitle", "Імпорт даних"),
                            MascotEmotion.Normal);
                    }
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!SettingsManager.Default.TutorialComplete && mainWindow.TutorialGrid != null)
                {
                    mainWindow.TutorialGrid.IsVisible = true;
                }
            });

            _hasCompletedSequence = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FirstRunCoordinator] Error: {ex.Message}");
        }
        finally
        {
            _runGate.Release();
        }
    }
}
