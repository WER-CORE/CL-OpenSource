using CL.Benchmarks.Models;

namespace CL.Benchmarks.Services;

public static class StatsCalculator
{
    public static double ComputeNormalizedCpuPercent(TimeSpan cpuDelta, double wallElapsedMs, int processorCount)
    {
        if (wallElapsedMs <= 0 || processorCount <= 0 || cpuDelta.TotalMilliseconds <= 0)
            return 0.0;

        var raw = (cpuDelta.TotalMilliseconds / (wallElapsedMs * processorCount)) * 100.0;
        return Math.Max(0.0, Math.Round(raw, 3));
    }

    public static double? CalculatePercentDiff(double baseline, double candidate)
    {
        if (double.IsNaN(baseline) || double.IsNaN(candidate) || double.IsInfinity(baseline) || double.IsInfinity(candidate))
            return null;

        if (Math.Abs(baseline) < 1e-12)
        {
            return Math.Abs(candidate) < 1e-12 ? 0.0 : null;
        }

        return Math.Round(((candidate - baseline) / baseline) * 100.0, 2);
    }

    public static AggregateMetric? CalculateAggregate(string name, string unit, IEnumerable<double> values)
    {
        var sorted = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();
        if (sorted.Count == 0)
            return null;

        double median = sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2.0;

        return new AggregateMetric
        {
            Name = name,
            Unit = unit,
            Min = Math.Round(sorted[0], 3),
            Max = Math.Round(sorted[^1], 3),
            Average = Math.Round(sorted.Average(), 3),
            Median = Math.Round(median, 3),
            SampleCount = sorted.Count
        };
    }

    public static TargetSummary SummarizeTarget(BuildTarget target, IReadOnlyList<RunResult> runs)
    {
        var successful = runs.Where(r => r.Success).ToList();
        var metrics = new List<AggregateMetric>();

        AddIfPresent(metrics, "ProcessStartMs", "ms", successful.Select(r => r.ProcessStartMs));
        AddIfPresent(metrics, "TimeToFirstValidSampleMs", "ms", successful.Where(r => r.TimeToFirstValidSampleMs.HasValue).Select(r => r.TimeToFirstValidSampleMs!.Value));
        AddIfPresent(metrics, "TotalProcessorTimeMs", "ms", successful.Where(r => r.TotalProcessorTimeMs.HasValue).Select(r => r.TotalProcessorTimeMs!.Value));
        AddIfPresent(metrics, "AvgCpuPercent", "%", successful.Where(r => r.AvgCpuPercent.HasValue).Select(r => r.AvgCpuPercent!.Value));
        AddIfPresent(metrics, "MaxCpuPercent", "%", successful.Where(r => r.MaxCpuPercent.HasValue).Select(r => r.MaxCpuPercent!.Value));
        AddIfPresent(metrics, "AvgWorkingSetMb", "MB", successful.Select(r => r.AvgWorkingSetMb));
        AddIfPresent(metrics, "PeakWorkingSetMb", "MB", successful.Where(r => r.PeakWorkingSetMb.HasValue).Select(r => r.PeakWorkingSetMb!.Value));
        AddIfPresent(metrics, "AvgPrivateMemoryMb", "MB", successful.Where(r => r.AvgPrivateMemoryMb.HasValue).Select(r => r.AvgPrivateMemoryMb!.Value));
        AddIfPresent(metrics, "MaxThreadCount", "threads", successful.Where(r => r.MaxThreadCount.HasValue).Select(r => (double)r.MaxThreadCount!.Value));

        return new TargetSummary
        {
            Target = target,
            SuccessfulRuns = successful.Count,
            FailedRuns = runs.Count - successful.Count,
            Metrics = metrics,
            Runs = runs
        };
    }

    public static IReadOnlyList<MetricDelta> CompareSummaries(TargetSummary baseline, TargetSummary candidate)
    {
        var baselineMap = baseline.Metrics.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var candidateMap = candidate.Metrics.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

        var allNames = baseline.Metrics.Select(m => m.Name)
            .Concat(candidate.Metrics.Select(m => m.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var deltas = new List<MetricDelta>();
        foreach (var name in allNames)
        {
            baselineMap.TryGetValue(name, out var b);
            candidateMap.TryGetValue(name, out var c);
            var unit = c?.Unit ?? b?.Unit ?? string.Empty;

            double? absDiff = (b is not null && c is not null) ? Math.Round(c.Average - b.Average, 3) : null;
            double? pctDiff = (b is not null && c is not null) ? CalculatePercentDiff(b.Average, c.Average) : null;

            deltas.Add(new MetricDelta
            {
                Name = name,
                Unit = unit,
                BaselineAverage = b?.Average,
                CandidateAverage = c?.Average,
                BaselineMedian = b?.Median,
                CandidateMedian = c?.Median,
                AbsoluteDiff = absDiff,
                PercentDiff = pctDiff
            });
        }

        return deltas;
    }

    private static void AddIfPresent(List<AggregateMetric> metrics, string name, string unit, IEnumerable<double> values)
    {
        var metric = CalculateAggregate(name, unit, values);
        if (metric is not null)
        {
            metrics.Add(metric);
        }
    }
}
