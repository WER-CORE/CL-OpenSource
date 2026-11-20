using System.Text.Json;
using CL.Benchmarks.Models;

namespace CL.Benchmarks.Services;

public static class HistoryStore
{
    private const string IndexFileName = "history.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void Append(string historyDirectory, HistoryEntry entry)
    {
        Directory.CreateDirectory(historyDirectory);
        var indexPath = Path.Combine(historyDirectory, IndexFileName);

        var entries = Load(historyDirectory, int.MaxValue).ToList();
        entries.Insert(0, entry);

        var json = JsonSerializer.Serialize(entries, JsonOptions);
        File.WriteAllText(indexPath, json);
    }

    public static IReadOnlyList<HistoryEntry> Load(string historyDirectory, int limit = 20)
    {
        var indexPath = Path.Combine(historyDirectory, IndexFileName);
        if (!File.Exists(indexPath))
            return Array.Empty<HistoryEntry>();

        try
        {
            var json = File.ReadAllText(indexPath);
            var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOptions);
            if (entries is null)
                return Array.Empty<HistoryEntry>();

            return entries.Take(Math.Max(1, limit)).ToList();
        }
        catch
        {
            return Array.Empty<HistoryEntry>();
        }
    }
}
