using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public class MemoryCleaner
    {
        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        private delegate int MallocTrimDelegate(int pad);
        private static MallocTrimDelegate? _mallocTrim;
        private static bool _mallocTrimInitialized;

        private delegate int MalloptDelegate(int param, int value);
        private static MalloptDelegate? _mallopt;
        private static bool _linuxAllocatorConfigured;

        private delegate int MadviseDelegate(IntPtr addr, UIntPtr length, int advice);
        private static MadviseDelegate? _madvise;
        private const int MADV_PAGEOUT = 21;

        private const int M_TRIM_THRESHOLD = -1;
        private const int M_TOP_PAD = -2;
        private const int M_MMAP_THRESHOLD = -3;
        private const int M_ARENA_MAX = -8;

        private delegate void MallocZonePressureReliefDelegate(IntPtr zone, IntPtr goal);
        private static MallocZonePressureReliefDelegate? _mallocZonePressureRelief;
        private static bool _macosReliefInitialized;

        public static Action? OnExtraCleanup { get; set; }

        public static void ConfigureLinuxAllocator()
        {
            if (!OperatingSystem.IsLinux() || _linuxAllocatorConfigured)
                return;

            _linuxAllocatorConfigured = true;
            try
            {
                EnsureLinuxFunctions();
                if (_mallopt != null)
                {
                    _mallopt(M_ARENA_MAX, 2);
                    _mallopt(M_TRIM_THRESHOLD, 64 * 1024);
                    _mallopt(M_MMAP_THRESHOLD, 64 * 1024);
                    _mallopt(M_TOP_PAD, 0);
                }
            }
            catch
            {
            }
        }

        public static async Task FlushMemoryAsync(bool trimWorkingSet = true)
        {
            await Task.Run(() => FlushMemory(trimWorkingSet));
        }

        public static void FlushMemory(bool trimWorkingSet = true)
        {
            try
            {
                OnExtraCleanup?.Invoke();

                var oldLatency = GCSettings.LatencyMode;
                try
                {
                    GCSettings.LatencyMode = GCLatencyMode.Batch;
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                    GC.WaitForPendingFinalizers();
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                }
                finally
                {
                    GCSettings.LatencyMode = oldLatency;
                }

                if (trimWorkingSet)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        var handle = GetCurrentProcess();
                        EmptyWorkingSet(handle);
                        SetProcessWorkingSetSize(handle, (IntPtr)(-1), (IntPtr)(-1));
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        TrimLinuxHeap();
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        TrimMacOsHeap();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[MemoryCleaner] Помилка очищення: {e.Message}");
            }
        }

        private static void EnsureLinuxFunctions()
        {
            if (_mallocTrimInitialized) return;
            _mallocTrimInitialized = true;

            string[] candidateLibs = { "libc.so.6", "libc.so", "libc" };
            foreach (var lib in candidateLibs)
            {
                if (NativeLibrary.TryLoad(lib, typeof(MemoryCleaner).Assembly, null, out var handle))
                {
                    if (NativeLibrary.TryGetExport(handle, "malloc_trim", out var trimPtr))
                    {
                        _mallocTrim = Marshal.GetDelegateForFunctionPointer<MallocTrimDelegate>(trimPtr);
                    }
                    if (NativeLibrary.TryGetExport(handle, "mallopt", out var malloptPtr))
                    {
                        _mallopt = Marshal.GetDelegateForFunctionPointer<MalloptDelegate>(malloptPtr);
                    }
                    if (NativeLibrary.TryGetExport(handle, "madvise", out var madvisePtr))
                    {
                        _madvise = Marshal.GetDelegateForFunctionPointer<MadviseDelegate>(madvisePtr);
                    }

                    if (_mallocTrim != null)
                        break;
                }
            }
        }

        private static void PageoutLinuxWorkingSet()
        {
            if (_madvise == null) return;

            try
            {
                if (!File.Exists("/proc/self/maps")) return;

                using var reader = new StreamReader("/proc/self/maps");
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    int spaceIdx = line.IndexOf(' ');
                    if (spaceIdx <= 0) continue;

                    string range = line.Substring(0, spaceIdx);
                    int dashIdx = range.IndexOf('-');
                    if (dashIdx <= 0) continue;

                    string perms = line.Substring(spaceIdx + 1, Math.Min(4, line.Length - spaceIdx - 1));
                    if (!perms.StartsWith("rw")) continue;

                    int lastSpace = line.LastIndexOf(' ');
                    if (lastSpace > 0 && lastSpace < line.Length - 1)
                    {
                        string name = line.Substring(lastSpace + 1);
                        if (name.StartsWith("[vsys") || name.StartsWith("[vdso") || name.StartsWith("[vvar"))
                            continue;
                    }

                    if (ulong.TryParse(range.Substring(0, dashIdx), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong start) &&
                        ulong.TryParse(range.Substring(dashIdx + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong end) &&
                        end > start)
                    {
                        UIntPtr size = (UIntPtr)(end - start);
                        _madvise((IntPtr)start, size, MADV_PAGEOUT);
                    }
                }
            }
            catch
            {
            }
        }

        private static void TrimLinuxHeap()
        {
            try
            {
                ConfigureLinuxAllocator();
                EnsureLinuxFunctions();
                _mallocTrim?.Invoke(0);
                PageoutLinuxWorkingSet();
            }
            catch
            {
            }
        }

        private static void TrimMacOsHeap()
        {
            try
            {
                if (!_macosReliefInitialized)
                {
                    _macosReliefInitialized = true;
                    string[] candidateLibs = { "/usr/lib/libSystem.B.dylib", "libSystem.B.dylib", "libSystem.dylib" };
                    foreach (var lib in candidateLibs)
                    {
                        if (NativeLibrary.TryLoad(lib, typeof(MemoryCleaner).Assembly, null, out var handle))
                        {
                            if (NativeLibrary.TryGetExport(handle, "malloc_zone_pressure_relief", out var funcPtr))
                            {
                                _mallocZonePressureRelief = Marshal.GetDelegateForFunctionPointer<MallocZonePressureReliefDelegate>(funcPtr);
                                break;
                            }
                        }
                    }
                }

                _mallocZonePressureRelief?.Invoke(IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
            }
        }
    }
}
