using System.ComponentModel;
using System.Diagnostics;
using CL.Benchmarks.Models;

namespace CL.Benchmarks.Services;

public sealed class ProcessSampler
{
    private const double BytesPerMb = 1024.0 * 1024.0;

    public async Task<RunResult> RunSampledAsync(
        BuildTarget target,
        int runIndex,
        int durationSeconds,
        int sampleIntervalMs,
        bool waitForUserEnter,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var samples = new List<SamplePoint>();
        Process? process = null;

        try
        {
            var startInfo = BuildStartInfo(target);
            process = new Process { StartInfo = startInfo };

            var wallWatch = Stopwatch.StartNew();
            if (!process.Start())
            {
                return FailedResult(target, runIndex, startedAtUtc, wallWatch.Elapsed.TotalMilliseconds, null, "Process.Start() returned false.", samples);
            }

            var processStartMs = wallWatch.Elapsed.TotalMilliseconds;
            double? timeToFirstValidSampleMs = null;

            TimeSpan? prevCpu = TryGetTotalProcessorTime(process);
            double prevWallMs = wallWatch.Elapsed.TotalMilliseconds;
            long? maxPeakWorkingSet = null;
            int? maxThreads = null;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(durationSeconds));

            Task? userEnterTask = waitForUserEnter && !Console.IsInputRedirected
                ? Task.Run(() => Console.ReadLine(), CancellationToken.None)
                : null;

            while (!timeoutCts.IsCancellationRequested)
            {
                if (userEnterTask is not null && userEnterTask.IsCompleted)
                {
                    break;
                }

                if (process.HasExited)
                {
                    var exitCode = SafeGetExitCode(process);
                    return FailedResult(
                        target,
                        runIndex,
                        startedAtUtc,
                        processStartMs,
                        exitCode,
                        $"Target process exited unexpectedly after {wallWatch.Elapsed.TotalMilliseconds:F0} ms with exit code {exitCode?.ToString() ?? "unknown"}.",
                        samples);
                }

                var sample = CaptureSample(process, wallWatch.Elapsed.TotalMilliseconds, ref prevCpu, ref prevWallMs);
                if (sample is not null)
                {
                    samples.Add(sample);
                    if (timeToFirstValidSampleMs is null && sample.WorkingSetBytes > 0)
                    {
                        timeToFirstValidSampleMs = sample.ElapsedMs;
                    }

                    if (sample.PeakWorkingSetBytes.HasValue)
                    {
                        maxPeakWorkingSet = Math.Max(maxPeakWorkingSet ?? 0, sample.PeakWorkingSetBytes.Value);
                    }

                    if (sample.ThreadCount.HasValue)
                    {
                        maxThreads = Math.Max(maxThreads ?? 0, sample.ThreadCount.Value);
                    }
                }

                try
                {
                    await Task.Delay(sampleIntervalMs, timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            if (!process.HasExited)
            {
                var finalSample = CaptureSample(process, wallWatch.Elapsed.TotalMilliseconds, ref prevCpu, ref prevWallMs);
                if (finalSample is not null)
                {
                    samples.Add(finalSample);
                    if (finalSample.PeakWorkingSetBytes.HasValue)
                    {
                        maxPeakWorkingSet = Math.Max(maxPeakWorkingSet ?? 0, finalSample.PeakWorkingSetBytes.Value);
                    }

                    if (finalSample.ThreadCount.HasValue)
                    {
                        maxThreads = Math.Max(maxThreads ?? 0, finalSample.ThreadCount.Value);
                    }
                }
            }

            var finalCpuMs = TryGetTotalProcessorTime(process)?.TotalMilliseconds;
            var cpuSamples = samples.Where(s => s.CpuPercent.HasValue).Select(s => s.CpuPercent!.Value).ToList();
            var privateSamples = samples.Where(s => s.PrivateMemoryBytes.HasValue).Select(s => s.PrivateMemoryBytes!.Value / BytesPerMb).ToList();
            var avgWsMb = samples.Count > 0 ? samples.Average(s => s.WorkingSetBytes / BytesPerMb) : 0.0;

            return new RunResult
            {
                Role = target.Role,
                Label = target.Label,
                RunIndex = runIndex,
                Success = true,
                ExitCode = 0,
                StartedAtUtc = startedAtUtc,
                ProcessStartMs = Math.Round(processStartMs, 2),
                TimeToFirstValidSampleMs = timeToFirstValidSampleMs.HasValue ? Math.Round(timeToFirstValidSampleMs.Value, 2) : null,
                TotalProcessorTimeMs = finalCpuMs.HasValue ? Math.Round(finalCpuMs.Value, 2) : null,
                AvgCpuPercent = cpuSamples.Count > 0 ? Math.Round(cpuSamples.Average(), 3) : null,
                MaxCpuPercent = cpuSamples.Count > 0 ? Math.Round(cpuSamples.Max(), 3) : null,
                AvgWorkingSetMb = Math.Round(avgWsMb, 3),
                PeakWorkingSetMb = maxPeakWorkingSet.HasValue ? Math.Round(maxPeakWorkingSet.Value / BytesPerMb, 3) : null,
                AvgPrivateMemoryMb = privateSamples.Count > 0 ? Math.Round(privateSamples.Average(), 3) : null,
                MaxThreadCount = maxThreads,
                Samples = samples
            };
        }
        catch (Exception ex) when (ex is Win32Exception or UnauthorizedAccessException or FileNotFoundException or InvalidOperationException)
        {
            return FailedResult(target, runIndex, startedAtUtc, 0, null, ex.Message, samples);
        }
        finally
        {
            TerminateOwnedProcess(process);
        }
    }

    private static ProcessStartInfo BuildStartInfo(BuildTarget target)
    {
        var isDotnetDll = target.ExecutablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var fileName = isDotnetDll ? "dotnet" : target.ExecutablePath;
        var arguments = isDotnetDll
            ? (string.IsNullOrWhiteSpace(target.Arguments)
                ? $"\"{target.ExecutablePath}\""
                : $"\"{target.ExecutablePath}\" {target.Arguments}")
            : target.Arguments;

        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(target.ExecutablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };
    }

    private static SamplePoint? CaptureSample(
        Process process,
        double elapsedMs,
        ref TimeSpan? prevCpu,
        ref double prevWallMs)
    {
        try
        {
            process.Refresh();
            if (process.HasExited)
                return null;

            long ws = process.WorkingSet64;
            long? privateBytes = TryReadLong(() => process.PrivateMemorySize64);
            long? peakWs = TryReadLong(() => process.PeakWorkingSet64);
            int? threads = TryReadInt(() => process.Threads.Count);

            var currentCpu = TryGetTotalProcessorTime(process);
            double? cpuPercent = null;
            if (currentCpu.HasValue && prevCpu.HasValue)
            {
                var wallDelta = elapsedMs - prevWallMs;
                if (wallDelta > 0)
                {
                    cpuPercent = StatsCalculator.ComputeNormalizedCpuPercent(
                        currentCpu.Value - prevCpu.Value,
                        wallDelta,
                        Environment.ProcessorCount);
                }
            }

            if (currentCpu.HasValue)
            {
                prevCpu = currentCpu;
                prevWallMs = elapsedMs;
            }

            return new SamplePoint
            {
                ElapsedMs = Math.Round(elapsedMs, 1),
                WorkingSetBytes = ws,
                PrivateMemoryBytes = privateBytes,
                PeakWorkingSetBytes = peakWs,
                CpuPercent = cpuPercent,
                ThreadCount = threads
            };
        }
        catch
        {
            return null;
        }
    }

    private static TimeSpan? TryGetTotalProcessorTime(Process process)
    {
        try
        {
            return process.TotalProcessorTime;
        }
        catch
        {
            return null;
        }
    }

    private static long? TryReadLong(Func<long> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static int? TryReadInt(Func<int> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static int? SafeGetExitCode(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TerminateOwnedProcess(Process? process)
    {
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.CloseMainWindow();
                    process.WaitForExit(1000);
                }
                catch
                {
                }

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    private static RunResult FailedResult(
        BuildTarget target,
        int runIndex,
        DateTimeOffset startedAtUtc,
        double processStartMs,
        int? exitCode,
        string errorMessage,
        IReadOnlyList<SamplePoint> samples)
    {
        return new RunResult
        {
            Role = target.Role,
            Label = target.Label,
            RunIndex = runIndex,
            Success = false,
            ExitCode = exitCode,
            ErrorMessage = errorMessage,
            StartedAtUtc = startedAtUtc,
            ProcessStartMs = Math.Round(processStartMs, 2),
            Samples = samples
        };
    }
}
