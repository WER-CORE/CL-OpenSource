using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public class MemoryCleaner
    {
        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        [DllImport("libc", EntryPoint = "malloc_trim", CallingConvention = CallingConvention.Cdecl)]
        private static extern int malloc_trim(int pad);

        public static async Task FlushMemoryAsync(bool trimWorkingSet = true)
        {
            await Task.Run(() => FlushMemory(trimWorkingSet));
        }
        public static void FlushMemory(bool trimWorkingSet = true)
        {
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);

                if (trimWorkingSet)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
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
