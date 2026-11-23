using System.Runtime.InteropServices;
using CL.Benchmarks.Models;
using CL.Benchmarks.Services;

namespace CL.Benchmarks.Scenarios;

public sealed class BenchmarkRunner
{
    private readonly ProcessSampler _sampler = new();

    public async Task<BenchmarkReport> ExecuteAsync(BenchmarkOptions options, CancellationToken cancellationToken = default)
    {
        var baselineTarget = VersionMetadataResolver.Resolve("Baseline", options.BaselineLabel, options.BaselinePath, options.BaselineArgs);
        var candidateTarget = VersionMetadataResolver.Resolve("Candidate", options.CandidateLabel, options.CandidatePath, options.CandidateArgs);

        var baselineRuns = new List<RunResult>();
        var candidateRuns = new List<RunResult>();

        var waitForEnter = options.Scenario == BenchmarkScenario.Navigation && !options.NonInteractive;

        for (int runIndex = 1; runIndex <= options.Runs; runIndex++)
        {
            var runCandidateFirst = runIndex % 2 == 0;
            var pair = runCandidateFirst
                ? new[] { (Target: candidateTarget, List: candidateRuns), (Target: baselineTarget, List: baselineRuns) }
                : new[] { (Target: baselineTarget, List: baselineRuns), (Target: candidateTarget, List: candidateRuns) };

            foreach (var item in pair)
            {
                Console.WriteLine($"[Run {runIndex}/{options.Runs}] Starting {item.Target.Role} ({item.Target.Label}) - Scenario: {options.Scenario}...");
                if (waitForEnter)
                {
                    Console.WriteLine("  -> Guided navigation: interact with the launcher window, then press [Enter] here to finish this run (or wait for timeout).");
                }

                var result = await _sampler.RunSampledAsync(
                    item.Target,
                    runIndex,
                    options.DurationSeconds,
                    options.SampleIntervalMs,
                    waitForEnter,
                    cancellationToken);

                item.List.Add(result);
                var statusText = result.Success
                    ? $"OK (Avg WS: {result.AvgWorkingSetMb:F2} MB, Avg CPU: {(result.AvgCpuPercent.HasValue ? $"{result.AvgCpuPercent:F2}%" : "N/A")})"
                    : $"FAILED ({result.ErrorMessage})";
                Console.WriteLine($"  -> Result: {statusText}");
            }
        }

        var baselineSummary = StatsCalculator.SummarizeTarget(baselineTarget, baselineRuns);
        var candidateSummary = StatsCalculator.SummarizeTarget(candidateTarget, candidateRuns);
        var deltas = StatsCalculator.CompareSummaries(baselineSummary, candidateSummary);

        var notes = new List<string>
        {
            "CPU% is normalized against Environment.ProcessorCount (0-100% of total machine capacity).",
            "UI readiness / first-frame rendering time is not directly measurable from an external process without in-app hooks; TimeToFirstValidSampleMs reflects first non-zero WorkingSet sample."
        };

        if (options.Scenario == BenchmarkScenario.Navigation)
        {
            notes.Add("Navigation scenario uses guided manual interaction between process start and user completion/timeout.");
        }

        return new BenchmarkReport
        {
            ReportId = $"bench-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Scenario = options.Scenario,
            RunsRequested = options.Runs,
            DurationSeconds = options.DurationSeconds,
            Machine = new MachineEnvironmentInfo
            {
                OsDescription = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                RuntimeVersion = RuntimeInformation.FrameworkDescription
            },
            Baseline = baselineSummary,
            Candidate = candidateSummary,
            Deltas = deltas,
            Notes = notes
        };
    }
}
