using CL.Benchmarks.Models;
using CL.Benchmarks.Reporting;
using CL.Benchmarks.Services;
using Xunit;

namespace CL.Benchmarks.Tests;

public sealed class ReportingAndHistoryTests
{
    [Fact]
    public void ReportWriters_ExportJsonCsvAndMarkdown()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cl-bench-report-{Guid.NewGuid():N}");
        try
        {
            var baseTarget = new BuildTarget { Role = "Baseline", Label = "1.1.3", ExecutablePath = "base.exe", Version = "1.1.3" };
            var candTarget = new BuildTarget { Role = "Candidate", Label = "1.1.4", ExecutablePath = "cand.exe", Version = "1.1.4" };

            var baseRuns = new[]
            {
                new RunResult { Role = "Baseline", Label = "1.1.3", RunIndex = 1, Success = true, ExitCode = 0, ProcessStartMs = 100, AvgWorkingSetMb = 200, AvgCpuPercent = 4 }
            };
            var candRuns = new[]
            {
                new RunResult { Role = "Candidate", Label = "1.1.4", RunIndex = 1, Success = true, ExitCode = 0, ProcessStartMs = 80, AvgWorkingSetMb = 150, AvgCpuPercent = 2 }
            };

            var bSummary = StatsCalculator.SummarizeTarget(baseTarget, baseRuns);
            var cSummary = StatsCalculator.SummarizeTarget(candTarget, candRuns);
            var deltas = StatsCalculator.CompareSummaries(bSummary, cSummary);

            var report = new BenchmarkReport
            {
                ReportId = "test-report-001",
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                Scenario = BenchmarkScenario.Idle,
                RunsRequested = 1,
                DurationSeconds = 5,
                Machine = new MachineEnvironmentInfo { OsDescription = "TestOS", Architecture = "X64", ProcessorCount = 8, RuntimeVersion = ".NET 10" },
                Baseline = bSummary,
                Candidate = cSummary,
                Deltas = deltas
            };

            var written = ReportWriters.WriteAll(report, tempDir);

            Assert.True(File.Exists(written.JsonPath));
            Assert.True(File.Exists(written.CsvPath));
            Assert.True(File.Exists(written.MarkdownPath));

            var md = File.ReadAllText(written.MarkdownPath);
            Assert.Contains("AvgWorkingSetMb", md);
            Assert.Contains("-25%", md);

            var csv = File.ReadAllText(written.CsvPath);
            Assert.Contains("Baseline,1.1.3,1.1.3,1,True", csv);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void HistoryStore_AppendsAndLoadsRecentEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"cl-bench-hist-{Guid.NewGuid():N}");
        try
        {
            HistoryStore.Append(tempDir, new HistoryEntry
            {
                ReportId = "r1",
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                Scenario = BenchmarkScenario.Startup,
                BaselineLabel = "A",
                BaselineVersion = "1.0",
                CandidateLabel = "B",
                CandidateVersion = "2.0",
                RunsRequested = 5,
                JsonPath = "r1.json",
                CsvPath = "r1.csv",
                MarkdownPath = "r1.md"
            });

            var loaded = HistoryStore.Load(tempDir, 10);
            Assert.Single(loaded);
            Assert.Equal("r1", loaded[0].ReportId);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
