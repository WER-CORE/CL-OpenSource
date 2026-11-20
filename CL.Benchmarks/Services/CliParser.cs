using CL.Benchmarks.Models;

namespace CL.Benchmarks.Services;

public sealed record CliParseResult(bool IsValid, BenchmarkOptions Options, string? ErrorMessage = null);

public static class CliParser
{
    public static CliParseResult Parse(string[] args, Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;

        if (args.Length == 0 || args.Any(a => a is "--help" or "-h" or "help"))
        {
            return new CliParseResult(true, new BenchmarkOptions { Mode = CommandMode.Help });
        }

        var command = args[0].Trim().ToLowerInvariant();
        if (command == "history")
        {
            return ParseHistory(args.Skip(1).ToArray());
        }

        if (command != "compare")
        {
            return new CliParseResult(false, new BenchmarkOptions(), $"Unknown command '{args[0]}'. Use 'compare', 'history', or '--help'.");
        }

        return ParseCompare(args.Skip(1).ToArray(), fileExists);
    }

    private static CliParseResult ParseHistory(string[] args)
    {
        var options = new BenchmarkOptions { Mode = CommandMode.History };
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--history-dir":
                    if (!TryReadValue(args, ref i, "--history-dir", out var historyDir, out var err))
                        return new CliParseResult(false, options, err);
                    options = options with { HistoryDirectory = historyDir };
                    break;
                case "--limit":
                    if (!TryReadInt(args, ref i, "--limit", 1, 1000, out var limit, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { HistoryLimit = limit };
                    break;
                default:
                    return new CliParseResult(false, options, $"Unknown option '{args[i]}' for 'history'.");
            }
        }

        return new CliParseResult(true, options);
    }

    private static CliParseResult ParseCompare(string[] args, Func<string, bool> fileExists)
    {
        var options = new BenchmarkOptions { Mode = CommandMode.Compare };
        bool durationExplicit = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--baseline":
                    if (!TryReadValue(args, ref i, "--baseline", out var baseline, out var err))
                        return new CliParseResult(false, options, err);
                    options = options with { BaselinePath = baseline };
                    break;
                case "--candidate":
                    if (!TryReadValue(args, ref i, "--candidate", out var candidate, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { CandidatePath = candidate };
                    break;
                case "--baseline-label":
                    if (!TryReadValue(args, ref i, "--baseline-label", out var bLabel, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { BaselineLabel = bLabel };
                    break;
                case "--candidate-label":
                    if (!TryReadValue(args, ref i, "--candidate-label", out var cLabel, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { CandidateLabel = cLabel };
                    break;
                case "--baseline-args":
                    if (!TryReadValue(args, ref i, "--baseline-args", out var bArgs, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { BaselineArgs = bArgs };
                    break;
                case "--candidate-args":
                    if (!TryReadValue(args, ref i, "--candidate-args", out var cArgs, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { CandidateArgs = cArgs };
                    break;
                case "--scenario":
                    if (!TryReadValue(args, ref i, "--scenario", out var scenarioRaw, out err))
                        return new CliParseResult(false, options, err);
                    if (!TryParseScenario(scenarioRaw, out var scenario))
                        return new CliParseResult(false, options, $"Invalid scenario '{scenarioRaw}'. Allowed: startup, idle, navigation.");
                    options = options with { Scenario = scenario };
                    break;
                case "--runs":
                    if (!TryReadInt(args, ref i, "--runs", 1, 100, out var runs, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { Runs = runs };
                    break;
                case "--duration":
                    if (!TryReadInt(args, ref i, "--duration", 1, 3600, out var duration, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { DurationSeconds = duration };
                    durationExplicit = true;
                    break;
                case "--interval-ms":
                    if (!TryReadInt(args, ref i, "--interval-ms", 50, 5000, out var intervalMs, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { SampleIntervalMs = intervalMs };
                    break;
                case "--output":
                    if (!TryReadValue(args, ref i, "--output", out var outputDir, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { OutputDirectory = outputDir };
                    break;
                case "--history-dir":
                    if (!TryReadValue(args, ref i, "--history-dir", out var historyDir, out err))
                        return new CliParseResult(false, options, err);
                    options = options with { HistoryDirectory = historyDir };
                    break;
                case "--non-interactive":
                    options = options with { NonInteractive = true };
                    break;
                default:
                    return new CliParseResult(false, options, $"Unknown option '{args[i]}' for 'compare'.");
            }
        }

        if (!durationExplicit && options.Scenario == BenchmarkScenario.Startup)
        {
            options = options with { DurationSeconds = 5 };
        }

        if (string.IsNullOrWhiteSpace(options.BaselinePath))
            return new CliParseResult(false, options, "Missing required argument '--baseline <path>'.");

        if (string.IsNullOrWhiteSpace(options.CandidatePath))
            return new CliParseResult(false, options, "Missing required argument '--candidate <path>'.");

        if (!fileExists(options.BaselinePath))
            return new CliParseResult(false, options, $"Baseline executable not found: '{options.BaselinePath}'.");

        if (!fileExists(options.CandidatePath))
            return new CliParseResult(false, options, $"Candidate executable not found: '{options.CandidatePath}'.");

        return new CliParseResult(true, options);
    }

    private static bool TryParseScenario(string raw, out BenchmarkScenario scenario)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "startup":
                scenario = BenchmarkScenario.Startup;
                return true;
            case "idle":
                scenario = BenchmarkScenario.Idle;
                return true;
            case "navigation":
                scenario = BenchmarkScenario.Navigation;
                return true;
            default:
                scenario = default;
                return false;
        }
    }

    private static bool TryReadValue(string[] args, ref int index, string optionName, out string value, out string? error)
    {
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            value = string.Empty;
            error = $"Option '{optionName}' requires a value.";
            return false;
        }

        index++;
        value = args[index];
        error = null;
        return true;
    }

    private static bool TryReadInt(string[] args, ref int index, string optionName, int min, int max, out int value, out string? error)
    {
        if (!TryReadValue(args, ref index, optionName, out var raw, out error))
        {
            value = 0;
            return false;
        }

        if (!int.TryParse(raw, out value) || value < min || value > max)
        {
            error = $"Option '{optionName}' must be an integer between {min} and {max}.";
            return false;
        }

        error = null;
        return true;
    }
}
