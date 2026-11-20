using CL.Benchmarks.Models;
using CL.Benchmarks.Services;
using Xunit;

namespace CL.Benchmarks.Tests;

public sealed class CliParserTests
{
    [Fact]
    public void Parse_NoArgsOrHelp_ReturnsHelpMode()
    {
        Assert.Equal(CommandMode.Help, CliParser.Parse(Array.Empty<string>()).Options.Mode);
        Assert.Equal(CommandMode.Help, CliParser.Parse(new[] { "--help" }).Options.Mode);
    }

    [Fact]
    public void Parse_ValidCompareArgs_ParsesAllFields()
    {
        var args = new[]
        {
            "compare",
            "--baseline", "a.exe",
            "--candidate", "b.exe",
            "--scenario", "startup",
            "--runs", "3",
            "--duration", "7",
            "--baseline-label", "v1",
            "--candidate-label", "v2",
            "--output", "outDir"
        };

        var res = CliParser.Parse(args, _ => true);

        Assert.True(res.IsValid, res.ErrorMessage);
        Assert.Equal(CommandMode.Compare, res.Options.Mode);
        Assert.Equal(BenchmarkScenario.Startup, res.Options.Scenario);
        Assert.Equal(3, res.Options.Runs);
        Assert.Equal(7, res.Options.DurationSeconds);
        Assert.Equal("v1", res.Options.BaselineLabel);
        Assert.Equal("v2", res.Options.CandidateLabel);
        Assert.Equal("outDir", res.Options.OutputDirectory);
    }

    [Fact]
    public void Parse_CompareMissingBaseline_FailsValidation()
    {
        var res = CliParser.Parse(new[] { "compare", "--candidate", "b.exe" }, _ => true);
        Assert.False(res.IsValid);
        Assert.Contains("--baseline", res.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareNonExistentPath_FailsValidation()
    {
        var res = CliParser.Parse(new[] { "compare", "--baseline", "missing.exe", "--candidate", "b.exe" }, p => p == "b.exe");
        Assert.False(res.IsValid);
        Assert.Contains("not found", res.ErrorMessage);
    }

    [Fact]
    public void Parse_InvalidScenarioOrRuns_FailsValidation()
    {
        var badScenario = CliParser.Parse(new[] { "compare", "--baseline", "a", "--candidate", "b", "--scenario", "unknown" }, _ => true);
        Assert.False(badScenario.IsValid);

        var badRuns = CliParser.Parse(new[] { "compare", "--baseline", "a", "--candidate", "b", "--runs", "0" }, _ => true);
        Assert.False(badRuns.IsValid);
    }

    [Fact]
    public void Parse_HistoryCommand_ParsesOptions()
    {
        var res = CliParser.Parse(new[] { "history", "--history-dir", "hist", "--limit", "5" });
        Assert.True(res.IsValid);
        Assert.Equal(CommandMode.History, res.Options.Mode);
        Assert.Equal("hist", res.Options.HistoryDirectory);
        Assert.Equal(5, res.Options.HistoryLimit);
    }
}
