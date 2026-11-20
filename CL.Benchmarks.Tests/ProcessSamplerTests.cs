using CL.Benchmarks.Models;
using CL.Benchmarks.Services;
using Xunit;

namespace CL.Benchmarks.Tests;

public sealed class ProcessSamplerTests
{
    [Fact]
    public async Task RunSampledAsync_NonExistentExecutable_ReturnsFailedResult()
    {
        var sampler = new ProcessSampler();
        var target = new BuildTarget
        {
            Role = "Baseline",
            Label = "missing",
            ExecutablePath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.exe")
        };

        var result = await sampler.RunSampledAsync(target, 1, 2, 100, waitForUserEnter: false);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task RunSampledAsync_EarlyExitProcess_RecordsFailureAndExitCode()
    {
        var sampler = new ProcessSampler();
        var target = new BuildTarget
        {
            Role = "Baseline",
            Label = "fast-exit",
            ExecutablePath = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? "/c exit 42" : "-c \"exit 42\""
        };

        var result = await sampler.RunSampledAsync(target, 1, 5, 100, waitForUserEnter: false);

        Assert.False(result.Success);
        Assert.Equal(42, result.ExitCode);
        Assert.Contains("exited unexpectedly", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunSampledAsync_LongRunningDummyProcess_SamplesAndTerminatesCleanly()
    {
        var sampler = new ProcessSampler();
        var target = new BuildTarget
        {
            Role = "Candidate",
            Label = "sleep-dummy",
            ExecutablePath = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows()
                ? "-NoProfile -Command \"Start-Sleep -Seconds 15\""
                : "-c \"sleep 15\""
        };

        var result = await sampler.RunSampledAsync(target, 1, 1, 150, waitForUserEnter: false);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotEmpty(result.Samples);
        Assert.True(result.AvgWorkingSetMb > 0);
        Assert.True(result.TimeToFirstValidSampleMs.HasValue);
    }
}
