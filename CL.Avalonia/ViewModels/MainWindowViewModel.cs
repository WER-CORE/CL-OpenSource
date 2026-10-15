using SukiUI.Dialogs;
using SukiUI.Toasts;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels
{
    public class MainWindowViewModel
    {
        public MainWindowViewModel(
            HomeViewModel home,
            AccountsViewModel accounts,
            SettingsViewModel settings,
            DownloadProgressViewModel progress,
            ISukiDialogManager dialogManager,
            ISukiToastManager toastManager)
        {
            Home = home;
            Accounts = accounts;
            Settings = settings;
            Progress = progress;
            DialogManager = dialogManager;
            ToastManager = toastManager;
        }

        public HomeViewModel Home { get; }
        public AccountsViewModel Accounts { get; }
        public SettingsViewModel Settings { get; }
        public DownloadProgressViewModel Progress { get; }
        public ISukiDialogManager DialogManager { get; }
        public ISukiToastManager ToastManager { get; }

        public async Task InitializeAsync()
        {
            await Accounts.LoadAsync();
            await Home.LoadVersionsAsync();
        }
    }
}
