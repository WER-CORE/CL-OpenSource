using DiscordRPC;
using System;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    class DiscordController
    {
        public static DiscordRpcClient clientdsc;

        public static async Task Initialize(string textDetails)
        {
            if (SettingsManager.Default.OfflineModLauncher || !SettingsManager.Default.EnableMod_DiscordRPC) { return; }

            try
            {
                clientdsc = new DiscordRpcClient("1210664596289884200");

                clientdsc.Initialize();

                clientdsc.SetPresence(new RichPresence()
                {
                    State = textDetails,
                    Details = LocalizationManager.GetString("DiscordRPC.Details", "Український лаунчер майнкрафт"),
                    Timestamps = Timestamps.Now,
                    Assets = new Assets()
                    {
                        LargeImageKey = "frame_73",
                        SmallImageKey = "ua",
                    },
                    Buttons = new[]
                    {
                        new Button()
                        {
                            Label = "Репозиторій",
                            Url = "https://github.com/WER-CORE/CL-OpenSource"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Discord RPC: {ex.Message}");
            }
        }

        public static async Task UpdatePresence(string textDetails)
        {
            if (SettingsManager.Default.OfflineModLauncher || !SettingsManager.Default.EnableMod_DiscordRPC) { return; }

            if (clientdsc == null)
            {
                Console.WriteLine("Discord RPC client not initialized.");
                return;
            }

            try
            {
                clientdsc.SetPresence(new RichPresence()
                {
                    State = textDetails,
                    Details = LocalizationManager.GetString("DiscordRPC.Details", "Український лаунчер майнкрафт"),
                    Timestamps = Timestamps.Now,
                    Assets = new Assets()
                    {
                        LargeImageKey = "frame_73",
                        SmallImageKey = "ua",
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Discord presence: {ex.Message}");
            }
        }

        public static void Deinitialize()
        {
            if (SettingsManager.Default.OfflineModLauncher || !SettingsManager.Default.EnableMod_DiscordRPC) { return; }

            try
            {
                clientdsc?.Dispose();
                clientdsc = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deinitializing Discord RPC: {ex.Message}");
            }
        }
    }
}