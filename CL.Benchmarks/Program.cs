using CL.Benchmarks.Models;
using CL.Benchmarks.Reporting;
using CL.Benchmarks.Scenarios;
using CL.Benchmarks.Services;

namespace CL.Benchmarks;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var parseResult = CliParser.Parse(args);
        if (!parseResult.IsValid)
        {
            Console.Error.WriteLine($"Error: {parseResult.ErrorMessage}");
            Console.Error.WriteLine("Run with --help to see usage.");
            return 1;
        }

        var options = parseResult.Options;
        switch (options.Mode)
        {
            case CommandMode.Help:
                PrintHelp();
                return 0;

            case CommandMode.History:
                return RunHistory(options);

            case CommandMode.Compare:
                return await RunCompareAsync(options);

            default:
                return 1;
        }
    }

    private static int RunHistory(BenchmarkOptions options)
    {
        var entries = HistoryStore.Load(options.HistoryDirectory, options.HistoryLimit);
        if (entries.Count == 0)
        {
            Console.WriteLine($"No benchmark history found in '{Path.GetFullPath(options.HistoryDirectory)}'.");
            return 0;
        }

        Console.WriteLine($"Recent benchmark runs ({entries.Count}):");
        foreach (var e in entries)
        {
            Console.WriteLine($"- {e.ReportId} | {e.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}Z | {e.Scenario} | {e.BaselineLabel} (v{e.BaselineVersion}) vs {e.CandidateLabel} (v{e.CandidateVersion}) | runs={e.RunsRequested}");
            Console.WriteLine($"  MD:   {e.MarkdownPath}");
            Console.WriteLine($"  JSON: {e.JsonPath}");
        }

        return 0;
    }

    private static async Task<int> RunCompareAsync(BenchmarkOptions options)
    {
        var runner = new BenchmarkRunner();
        var report = await runner.ExecuteAsync(options);
        var files = ReportWriters.WriteAll(report, options.OutputDirectory);

        HistoryStore.Append(options.HistoryDirectory, new HistoryEntry
        {
            ReportId = report.ReportId,
            GeneratedAtUtc = report.GeneratedAtUtc,
            Scenario = report.Scenario,
            BaselineLabel = report.Baseline.Target.Label,
            BaselineVersion = report.Baseline.Target.Version,
            CandidateLabel = report.Candidate.Target.Label,
            CandidateVersion = report.Candidate.Target.Version,
            RunsRequested = report.RunsRequested,
            JsonPath = Path.GetFullPath(files.JsonPath),
            CsvPath = Path.GetFullPath(files.CsvPath),
            MarkdownPath = Path.GetFullPath(files.MarkdownPath)
        });

        Console.WriteLine();
        Console.WriteLine("=== Benchmark Complete ===");
        Console.WriteLine($"JSON:     {Path.GetFullPath(files.JsonPath)}");
        Console.WriteLine($"CSV:      {Path.GetFullPath(files.CsvPath)}");
        Console.WriteLine($"Markdown: {Path.GetFullPath(files.MarkdownPath)}");

        var anySuccess = report.Baseline.SuccessfulRuns > 0 || report.Candidate.SuccessfulRuns > 0;
        return anySuccess ? 0 : 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
CL.Benchmarks — Universal A/B performance comparison tool for any two CL builds

USAGE:
  CL.Benchmarks compare --baseline <path> --candidate <path> [options]
  CL.Benchmarks history [--history-dir <dir>] [--limit <n>]
  CL.Benchmarks --help

COMPARE OPTIONS:
  --baseline <path>          Path to baseline executable or .dll (required)
  --candidate <path>         Path to candidate executable or .dll (required)
  --scenario <name>          startup | idle | navigation (default: idle)
  --runs <n>                 Number of paired runs per build (1..100, default: 5)
  --duration <sec>           Observation duration per run in seconds (default: 15, startup: 5)
  --interval-ms <ms>         Sampling interval in milliseconds (50..5000, default: 250)
  --output <dir>             Directory for JSON/CSV/MD reports (default: CL.Benchmarks/results)
  --history-dir <dir>        Directory for history index (default: CL.Benchmarks/history)
  --baseline-label <name>    Display label for baseline (default: Baseline)
  --candidate-label <name>   Display label for candidate (default: Candidate)
  --baseline-args <args>     Optional CLI arguments passed to baseline process
  --candidate-args <args>    Optional CLI arguments passed to candidate process
  --non-interactive          Do not wait for [Enter] in navigation scenario

EXAMPLES:
  CL.Benchmarks compare --baseline C:\builds\1.1.3\CL.Avalonia.exe --candidate C:\builds\1.1.4\CL.Avalonia.exe --scenario idle --runs 5 --duration 15 --baseline-label 1.1.3 --candidate-label 1.1.4
  CL.Benchmarks history --limit 10
""");
    }
}
