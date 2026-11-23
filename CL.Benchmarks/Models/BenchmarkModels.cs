namespace CL.Benchmarks.Models;

public enum BenchmarkScenario
{
    Startup,
    Idle,
    Navigation
}

public enum CommandMode
{
    Help,
    Compare,
    History
}

public sealed record BenchmarkOptions
{
    public CommandMode Mode { get; init; } = CommandMode.Help;
    public string BaselinePath { get; init; } = string.Empty;
    public string CandidatePath { get; init; } = string.Empty;
    public string BaselineLabel { get; init; } = "Baseline";
    public string CandidateLabel { get; init; } = "Candidate";
    public string BaselineArgs { get; init; } = string.Empty;
    public string CandidateArgs { get; init; } = string.Empty;
    public BenchmarkScenario Scenario { get; init; } = BenchmarkScenario.Idle;
    public int Runs { get; init; } = 5;
    public int DurationSeconds { get; init; } = 15;
    public int SampleIntervalMs { get; init; } = 250;
    public string OutputDirectory { get; init; } = Path.Combine("CL.Benchmarks", "results");
    public string HistoryDirectory { get; init; } = Path.Combine("CL.Benchmarks", "history");
    public int HistoryLimit { get; init; } = 20;
    public bool NonInteractive { get; init; }
}

public sealed record BuildTarget
{
    public required string Role { get; init; }
    public required string Label { get; init; }
    public required string ExecutablePath { get; init; }
    public string Arguments { get; init; } = string.Empty;
    public string Version { get; init; } = "unknown";
    public string? ProductVersion { get; init; }
    public string? FileVersion { get; init; }
}

public sealed record SamplePoint
{
    public double ElapsedMs { get; init; }
    public long WorkingSetBytes { get; init; }
    public long? PrivateMemoryBytes { get; init; }
    public long? PeakWorkingSetBytes { get; init; }
    public double? CpuPercent { get; init; }
    public int? ThreadCount { get; init; }
}

public sealed record RunResult
{
    public required string Role { get; init; }
    public required string Label { get; init; }
    public int RunIndex { get; init; }
    public bool Success { get; init; }
    public int? ExitCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public double ProcessStartMs { get; init; }
    public double? TimeToFirstValidSampleMs { get; init; }
    public double? TotalProcessorTimeMs { get; init; }
    public double? AvgCpuPercent { get; init; }
    public double? MaxCpuPercent { get; init; }
    public double AvgWorkingSetMb { get; init; }
    public double? PeakWorkingSetMb { get; init; }
    public double? AvgPrivateMemoryMb { get; init; }
    public int? MaxThreadCount { get; init; }
    public IReadOnlyList<SamplePoint> Samples { get; init; } = Array.Empty<SamplePoint>();
}

public sealed record AggregateMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Average { get; init; }
    public double Median { get; init; }
    public int SampleCount { get; init; }
}

public sealed record MetricDelta
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public double? BaselineAverage { get; init; }
    public double? CandidateAverage { get; init; }
    public double? BaselineMedian { get; init; }
    public double? CandidateMedian { get; init; }
    public double? AbsoluteDiff { get; init; }
    public double? PercentDiff { get; init; }
}

public sealed record TargetSummary
{
    public required BuildTarget Target { get; init; }
    public int SuccessfulRuns { get; init; }
    public int FailedRuns { get; init; }
    public IReadOnlyList<AggregateMetric> Metrics { get; init; } = Array.Empty<AggregateMetric>();
    public IReadOnlyList<RunResult> Runs { get; init; } = Array.Empty<RunResult>();
}

public sealed record MachineEnvironmentInfo
{
    public required string OsDescription { get; init; }
    public required string Architecture { get; init; }
    public int ProcessorCount { get; init; }
    public required string RuntimeVersion { get; init; }
}

public sealed record BenchmarkReport
{
    public required string ReportId { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public BenchmarkScenario Scenario { get; init; }
    public int RunsRequested { get; init; }
    public int DurationSeconds { get; init; }
    public required MachineEnvironmentInfo Machine { get; init; }
    public required TargetSummary Baseline { get; init; }
    public required TargetSummary Candidate { get; init; }
    public IReadOnlyList<MetricDelta> Deltas { get; init; } = Array.Empty<MetricDelta>();
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record HistoryEntry
{
    public required string ReportId { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public BenchmarkScenario Scenario { get; init; }
    public required string BaselineLabel { get; init; }
    public required string BaselineVersion { get; init; }
    public required string CandidateLabel { get; init; }
    public required string CandidateVersion { get; init; }
    public int RunsRequested { get; init; }
    public required string JsonPath { get; init; }
    public required string CsvPath { get; init; }
    public required string MarkdownPath { get; init; }
}
