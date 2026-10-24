using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using CL.Core.Interfaces;
using CL.Core.Services;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class AvaloniaSoundService : ISoundService
{
    private static readonly ConcurrentDictionary<string, byte[]> MemoryAudioCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> TempFileAudioCache = new(StringComparer.OrdinalIgnoreCase);

    public static AvaloniaSoundService? Current { get; private set; }

    public AvaloniaSoundService()
    {
        Current = this;
    }

    public void PlayClick() => PlaySound("click");

    public void PlaySound(string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName)) return;

        if (resourceName.Equals("click", StringComparison.OrdinalIgnoreCase))
        {
            if (!ShouldPlayClick()) return;
        }

        PlaySoundInternal(resourceName);
    }

    private void PlaySoundInternal(string resourceName)
    {
        try
        {
            if (!SettingsManager.Default.EnableSound) return;

            if (OperatingSystem.IsWindows())
            {
                PlayWindowsSound(resourceName);
            }
            else if (OperatingSystem.IsMacOS())
            {
                _ = PlayMacSoundAsync(resourceName);
            }
            else if (OperatingSystem.IsLinux())
            {
                _ = PlayLinuxSoundAsync(resourceName);
            }
            else
            {
                Debug.WriteLine($"[SoundService] Платформа не підтримує аудіо: {resourceName}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SoundService] Помилка відтворення звуку '{resourceName}': {ex.Message}");
        }
    }

    public void StopSound()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                WindowsAudioInterop.PlaySound(null, IntPtr.Zero, WindowsAudioInterop.SND_PURGE);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SoundService] Помилка зупинки звуку: {ex.Message}");
        }
    }

    #region Windows Native Playback (winmm.dll)

    private static void PlayWindowsSound(string soundName)
    {
        try
        {
            var audioBytes = GetOrCreateAudioBytes(soundName);
            if (audioBytes == null || audioBytes.Length == 0) return;

            const uint flags = WindowsAudioInterop.SND_ASYNC |
                               WindowsAudioInterop.SND_MEMORY |
                               WindowsAudioInterop.SND_NODEFAULT;

            WindowsAudioInterop.PlaySound(audioBytes, IntPtr.Zero, flags);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SoundService] Windows playback error: {ex.Message}");
        }
    }

    private static class WindowsAudioInterop
    {
        public const uint SND_ASYNC = 0x0001;
        public const uint SND_NODEFAULT = 0x0002;
        public const uint SND_MEMORY = 0x0004;
        public const uint SND_PURGE = 0x0040;

        [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true, ExactSpelling = true)]
        public static extern bool PlaySound(byte[]? pszSound, IntPtr hmod, uint fdwSound);
    }

    #endregion

    #region Linux & macOS Process Playback

    private static async Task PlayMacSoundAsync(string soundName)
    {
        try
        {
            string? tempFilePath = await GetOrCreateTempAudioFileAsync(soundName);
            if (string.IsNullOrEmpty(tempFilePath) || !File.Exists(tempFilePath)) return;

            await Task.Run(() =>
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "afplay",
                    Arguments = $"\"{tempFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SoundService] macOS playback error: {ex.Message}");
        }
    }

    private static async Task PlayLinuxSoundAsync(string soundName)
    {
        try
        {
            string? tempFilePath = await GetOrCreateTempAudioFileAsync(soundName);
            if (string.IsNullOrEmpty(tempFilePath) || !File.Exists(tempFilePath)) return;

            await Task.Run(() =>
            {

                string[] players = ["pw-play", "paplay", "aplay"];
                foreach (var player in players)
                {
                    try
                    {
                        using var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = player,
                            Arguments = $"\"{tempFilePath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });

                        if (process != null) break;
                    }
                    catch
                    {

                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SoundService] Linux playback error: {ex.Message}");
        }
    }

    #endregion

    #region Asset Loading & Cache

    private static byte[]? GetOrCreateAudioBytes(string soundName)
    {
        if (MemoryAudioCache.TryGetValue(soundName, out var cachedBytes))
        {
            return cachedBytes;
        }

        try
        {
            var soundUri = new Uri($"avares://CL.Avalonia/Assets/Sounds/{soundName}.wav");
            if (!AssetLoader.Exists(soundUri)) return null;

            using var stream = AssetLoader.Open(soundUri);
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var bytes = memoryStream.ToArray();

            MemoryAudioCache.TryAdd(soundName, bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SoundService] Failed to load audio asset '{soundName}': {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> GetOrCreateTempAudioFileAsync(string soundName)
    {
        if (TempFileAudioCache.TryGetValue(soundName, out var cachedPath) && File.Exists(cachedPath))
        {
            return cachedPath;
        }

        try
        {
            var bytes = GetOrCreateAudioBytes(soundName);
            if (bytes == null || bytes.Length == 0) return null;

            string tempDir = Path.Combine(Path.GetTempPath(), "CLEGENDARY_SOUNDS");
            Directory.CreateDirectory(tempDir);

            string filePath = Path.Combine(tempDir, $"{soundName}.wav");
            await File.WriteAllBytesAsync(filePath, bytes);

            TempFileAudioCache.TryAdd(soundName, filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SoundService] Failed to create temp audio file '{soundName}': {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Static Facades

    private static readonly object _clickLock = new();
    private static long _lastClickTimestamp;
    public const int ClickDebounceMs = 150;

    public static bool ShouldPlayClick()
    {
        lock (_clickLock)
        {
            long now = Environment.TickCount64;
            if (now - _lastClickTimestamp < ClickDebounceMs) return false;
            _lastClickTimestamp = now;
            return true;
        }
    }

    public static void Click()
    {
        if (!ShouldPlayClick()) return;

        if (Current != null)
        {
            Current.PlaySoundInternal("click");
            return;
        }

        var app = Application.Current as App;
        var soundService = app?.ServiceProvider?.GetService(typeof(ISoundService)) as ISoundService;
        soundService?.PlaySound("click");
    }

    public static void Play(string soundName)
    {
        if (Current != null)
        {
            Current.PlaySound(soundName);
            return;
        }

        var app = Application.Current as App;
        var soundService = app?.ServiceProvider?.GetService(typeof(ISoundService)) as ISoundService;
        soundService?.PlaySound(soundName);
    }

    #endregion
}
