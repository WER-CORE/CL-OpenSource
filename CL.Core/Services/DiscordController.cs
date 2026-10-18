using DiscordRPC;
using System;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public class DiscordController
    {
        public const string ApplicationId = "1210664596289884200";

        public static DiscordRpcClient? clientdsc;
        private static readonly object _syncRoot = new();
        private static Timestamps? _startTime;

        public static Task Initialize(string textDetails = "")
        {
            if (SettingsManager.Default.OfflineModLauncher || !SettingsManager.Default.EnableMod_DiscordRPC)
            {
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                lock (_syncRoot)
                {
                    try
                    {
                        if (clientdsc != null && !clientdsc.IsDisposed)
                        {
                            UpdatePresenceInternal(textDetails);
                            return;
                        }

                        _startTime = Timestamps.Now;
                        clientdsc = new DiscordRpcClient(ApplicationId);
                        clientdsc.Initialize();
                        UpdatePresenceInternal(textDetails);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error initializing Discord RPC: {ex.Message}");
                    }
                }
            });
        }

        public static Task UpdatePresence(string textState, string? customDetails = null, bool resetTimer = false)
        {
            if (SettingsManager.Default.OfflineModLauncher || !SettingsManager.Default.EnableMod_DiscordRPC)
            {
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                lock (_syncRoot)
                {
                    try
                    {
                        if (resetTimer || _startTime == null)
                        {
                            _startTime = Timestamps.Now;
                        }

                        if (clientdsc == null || clientdsc.IsDisposed)
                        {
                            clientdsc = new DiscordRpcClient(ApplicationId);
                            clientdsc.Initialize();
                        }

                        UpdatePresenceInternal(textState, customDetails);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating Discord presence: {ex.Message}");
                    }
                }
            });
        }

        private static void UpdatePresenceInternal(string textState, string? customDetails = null)
        {
            if (clientdsc == null || clientdsc.IsDisposed) return;

            string details = !string.IsNullOrWhiteSpace(customDetails)
                ? customDetails
                : LocalizationManager.GetString("DiscordRPC.Details", "Український лаунчер майнкрафт");

            clientdsc.SetPresence(new RichPresence()
            {
                State = textState,
                Details = details,
                Timestamps = _startTime ?? Timestamps.Now,
                Assets = new Assets()
                {
                    LargeImageKey = "frame_73",
                    LargeImageText = "CLegendary Launcher",
                    SmallImageKey = "ua",
                    SmallImageText = "Україна"
                }
            });
        }

        public static void Deinitialize()
        {
            lock (_syncRoot)
            {
                try
                {
                    if (clientdsc != null)
                    {
                        if (!clientdsc.IsDisposed)
                        {
                            clientdsc.ClearPresence();
                            clientdsc.Dispose();
                        }
                        clientdsc = null;
                    }
                    _startTime = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deinitializing Discord RPC: {ex.Message}");
                }
            }
        }
    }
}
