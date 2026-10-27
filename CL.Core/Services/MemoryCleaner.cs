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

        [DllImport("libc", EntryPoint = "malloc_trim", CallingConvention = CallingConvention.Cdecl)]
        private static extern int malloc_trim(int pad);

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
                        try
                        {
                            malloc_trim(0);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[MemoryCleaner] Помилка очищення: {e.Message}");
            }
        }
    }
}
