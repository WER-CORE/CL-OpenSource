using System.Globalization;
using System.Text;
using System.Text.Json;
using CL.Benchmarks.Models;

namespace CL.Benchmarks.Reporting;

public sealed record WrittenReportFiles(string JsonPath, string CsvPath, string MarkdownPath);

public static class ReportWriters
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static WrittenReportFiles WriteAll(BenchmarkReport report, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var jsonPath = Path.Combine(outputDirectory, $"{report.ReportId}.json");
        var csvPath = Path.Combine(outputDirectory, $"{report.ReportId}.csv");
        var mdPath = Path.Combine(outputDirectory, $"{report.ReportId}.md");

        File.WriteAllText(jsonPath, SerializeJson(report));
        File.WriteAllText(csvPath, BuildCsv(report));
        File.WriteAllText(mdPath, BuildMarkdown(report));

        return new WrittenReportFiles(jsonPath, csvPath, mdPath);
    }

    public static string SerializeJson(BenchmarkReport report) => JsonSerializer.Serialize(report, JsonOptions);

    public static string BuildCsv(BenchmarkReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Role,Label,Version,RunIndex,Success,ExitCode,ProcessStartMs,TimeToFirstValidSampleMs,TotalProcessorTimeMs,AvgCpuPercent,MaxCpuPercent,AvgWorkingSetMb,PeakWorkingSetMb,AvgPrivateMemoryMb,MaxThreadCount,ErrorMessage");

        foreach (var run in report.Baseline.Runs.Concat(report.Candidate.Runs))
        {
            var version = run.Role == "Baseline" ? report.Baseline.Target.Version : report.Candidate.Target.Version;
            sb.AppendLine(string.Join(",",
                CsvEscape(run.Role),
                CsvEscape(run.Label),
                CsvEscape(version),
                run.RunIndex.ToString(CultureInfo.InvariantCulture),
                run.Success.ToString(CultureInfo.InvariantCulture),
                FormatNullable(run.ExitCode),
                run.ProcessStartMs.ToString("F2", CultureInfo.InvariantCulture),
                FormatNullable(run.TimeToFirstValidSampleMs),
                FormatNullable(run.TotalProcessorTimeMs),
                FormatNullable(run.AvgCpuPercent),
                FormatNullable(run.MaxCpuPercent),
                run.AvgWorkingSetMb.ToString("F3", CultureInfo.InvariantCulture),
                FormatNullable(run.PeakWorkingSetMb),
                FormatNullable(run.AvgPrivateMemoryMb),
                FormatNullable(run.MaxThreadCount),
                CsvEscape(run.ErrorMessage ?? string.Empty)));
        }

        return sb.ToString();
    }

    public static string BuildMarkdown(BenchmarkReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# CL Benchmark Report (`{report.ReportId}`)");
        sb.AppendLine();
        sb.AppendLine($"- **Generated (UTC):** {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}Z");
        sb.AppendLine($"- **Scenario:** `{report.Scenario}` | **Runs:** {report.RunsRequested} | **Duration:** {report.DurationSeconds}s");
        sb.AppendLine($"- **Environment:** {report.Machine.OsDescription} ({report.Machine.Architecture}, {report.Machine.ProcessorCount} logical cores, {report.Machine.RuntimeVersion})");
        sb.AppendLine($"- **Baseline:** `{report.Baseline.Target.Label}` (v{report.Baseline.Target.Version}) — {report.Baseline.SuccessfulRuns}/{report.RunsRequested} OK");
        sb.AppendLine($"- **Candidate:** `{report.Candidate.Target.Label}` (v{report.Candidate.Target.Version}) — {report.Candidate.SuccessfulRuns}/{report.RunsRequested} OK");
        sb.AppendLine();
        sb.AppendLine("## Comparison Summary");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Unit | {report.Baseline.Target.Label} Avg | {report.Candidate.Target.Label} Avg | Diff | Diff % | Baseline Median | Candidate Median |");
        sb.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");

        foreach (var d in report.Deltas)
        {
            var pct = d.PercentDiff.HasValue ? $"{d.PercentDiff.Value:+0.##;-0.##;0}%" : "N/A";
            var abs = d.AbsoluteDiff.HasValue ? $"{d.AbsoluteDiff.Value:+0.###;-0.###;0}" : "N/A";
            sb.AppendLine($"| {d.Name} | {d.Unit} | {FormatNum(d.BaselineAverage)} | {FormatNum(d.CandidateAverage)} | {abs} | {pct} | {FormatNum(d.BaselineMedian)} | {FormatNum(d.CandidateMedian)} |");
        }

        if (report.Notes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Notes");
            foreach (var note in report.Notes)
            {
                sb.AppendLine($"- {note}");
            }
        }

        return sb.ToString();
    }

    private static string FormatNum(double? value) =>
        value.HasValue ? value.Value.ToString("F3", CultureInfo.InvariantCulture) : "N/A";

    private static string FormatNullable(double? value) =>
        value.HasValue ? value.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty;

    private static string FormatNullable(int? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
