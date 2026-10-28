using System;
using System.Diagnostics;
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

        private delegate void MallocZonePressureReliefDelegate(IntPtr zone, IntPtr goal);
        private static MallocZonePressureReliefDelegate? _mallocZonePressureRelief;
        private static bool _macosReliefInitialized;

        public static Action? OnExtraCleanup { get; set; }

        public static async Task FlushMemoryAsync(bool trimWorkingSet = true)
        {
            await Task.Run(() => FlushMemory(trimWorkingSet));
        }

        public static void FlushMemory(bool trimWorkingSet = true)
        {
            try
            {
                OnExtraCleanup?.Invoke();

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

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

        private static void TrimLinuxHeap()
        {
            try
            {
                if (!_mallocTrimInitialized)
                {
                    _mallocTrimInitialized = true;
                    string[] candidateLibs = { "libc.so.6", "libc.so", "libc" };
                    foreach (var lib in candidateLibs)
                    {
                        if (NativeLibrary.TryLoad(lib, typeof(MemoryCleaner).Assembly, null, out var handle))
                        {
                            if (NativeLibrary.TryGetExport(handle, "malloc_trim", out var funcPtr))
                            {
                                _mallocTrim = Marshal.GetDelegateForFunctionPointer<MallocTrimDelegate>(funcPtr);
                                break;
                            }
                        }
                    }
                }

                _mallocTrim?.Invoke(0);
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
