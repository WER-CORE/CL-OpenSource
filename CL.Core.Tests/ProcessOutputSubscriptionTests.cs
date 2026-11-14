using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    public class ProcessOutputSubscriptionTests
    {
        private static Process CreateSilentProcess()
        {
            var startInfo = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("cmd.exe", "/c echo hello-cl-log & ping -n 2 127.0.0.1 >nul")
                : new ProcessStartInfo("sh", "-c \"echo hello-cl-log; sleep 1\"");

            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            return new Process { StartInfo = startInfo };
        }

        [Fact]
        public void Attach_TwiceSameProcess_RemainsSingleAttached()
        {
            using var process = new Process();
            using var sub = new ProcessOutputSubscription(_ => { });

            sub.Attach(process);
            sub.Attach(process);

            Assert.True(sub.IsAttached);
        }

        [Fact]
        public void Dispose_DetachesWithoutKillingProcess_AndBlocksReattach()
        {
            using var process = CreateSilentProcess();
            var received = new List<string>();
            var sub = new ProcessOutputSubscription(line =>
            {
                lock (received) received.Add(line);
            });

            sub.Attach(process);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            sub.Dispose();

            Assert.False(sub.IsAttached);
            Assert.True(sub.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => sub.Attach(process));

            bool exited = process.WaitForExit(10_000);
            Assert.True(exited);
        }

        [Fact]
        public async Task ReopenConsoleWithNewSubscription_CapturesNewProcessOutput()
        {
            var firstLines = new List<string>();
            using (var p1 = CreateSilentProcess())
            using (var sub1 = new ProcessOutputSubscription(l => { lock (firstLines) firstLines.Add(l); }))
            {
                sub1.Attach(p1);
                p1.Start();
                p1.BeginOutputReadLine();
                p1.BeginErrorReadLine();
                await p1.WaitForExitAsync();
            }

            var secondLines = new List<string>();
            using (var p2 = CreateSilentProcess())
            using (var sub2 = new ProcessOutputSubscription(l => { lock (secondLines) secondLines.Add(l); }))
            {
                sub2.Attach(p2);
                p2.Start();
                p2.BeginOutputReadLine();
                p2.BeginErrorReadLine();
                await p2.WaitForExitAsync();
            }

            Assert.Contains(firstLines, l => l.Contains("hello-cl-log"));
            Assert.Contains(secondLines, l => l.Contains("hello-cl-log"));
        }

        [Fact]
        public async Task Attach_TwiceOnSameStartedProcess_DeliversOutputOnlyOnce()
        {
            using var process = CreateSilentProcess();
            int count = 0;
            using var sub = new ProcessOutputSubscription(line =>
            {
                if (line.Contains("hello-cl-log"))
                {
                    System.Threading.Interlocked.Increment(ref count);
                }
            });

            sub.Attach(process);
            sub.Attach(process);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            Assert.Equal(1, count);
        }
    }
}
