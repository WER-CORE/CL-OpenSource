using CL.Benchmarks.Services;
using Xunit;

namespace CL.Benchmarks.Tests;

public sealed class StatsCalculatorTests
{
    [Fact]
    public void ComputeNormalizedCpuPercent_NormalizesByProcessorCount()
    {
        var pct = StatsCalculator.ComputeNormalizedCpuPercent(TimeSpan.FromMilliseconds(500), 1000.0, 4);
        Assert.Equal(12.5, pct);
    }

    [Fact]
    public void CalculateAggregate_ComputesMinMaxAverageAndMedianForOddAndEvenCounts()
    {
        var odd = StatsCalculator.CalculateAggregate("M", "ms", new[] { 30.0, 10.0, 20.0 });
        Assert.NotNull(odd);
        Assert.Equal(10.0, odd!.Min);
        Assert.Equal(30.0, odd.Max);
        Assert.Equal(20.0, odd.Average);
        Assert.Equal(20.0, odd.Median);

        var even = StatsCalculator.CalculateAggregate("M", "ms", new[] { 40.0, 10.0, 20.0, 30.0 });
        Assert.NotNull(even);
        Assert.Equal(25.0, even!.Median);
        Assert.Equal(25.0, even.Average);
    }

    [Fact]
    public void CalculatePercentDiff_HandlesNormalAndZeroBaselinesSafely()
    {
        Assert.Equal(-25.0, StatsCalculator.CalculatePercentDiff(100.0, 75.0));
        Assert.Equal(50.0, StatsCalculator.CalculatePercentDiff(100.0, 150.0));
        Assert.Equal(0.0, StatsCalculator.CalculatePercentDiff(0.0, 0.0));
        Assert.Null(StatsCalculator.CalculatePercentDiff(0.0, 10.0));
    }
}
