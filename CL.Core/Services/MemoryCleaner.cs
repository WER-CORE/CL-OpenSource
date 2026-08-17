using System;
using System.Diagnostics;
using System.Runtime; 
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public class MemoryCleaner
    {
        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

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

                if (trimWorkingSet && Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[MemoryCleaner] Помилка очищення: {e.Message}");
            }
        }
    }
}