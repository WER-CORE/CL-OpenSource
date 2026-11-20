using System.Diagnostics;
using CL.Benchmarks.Models;

namespace CL.Benchmarks.Services;

public static class VersionMetadataResolver
{
    public static BuildTarget Resolve(string role, string label, string executablePath, string arguments)
    {
        var fullPath = Path.GetFullPath(executablePath);
        string? productVersion = null;
        string? fileVersion = null;
        string version = "unknown";

        try
        {
            var info = FileVersionInfo.GetVersionInfo(fullPath);
            productVersion = Normalize(info.ProductVersion);
            fileVersion = Normalize(info.FileVersion);
            version = productVersion ?? fileVersion ?? "unknown";
        }
        catch
        {
        }

        return new BuildTarget
        {
            Role = role,
            Label = label,
            ExecutablePath = fullPath,
            Arguments = arguments,
            Version = version,
            ProductVersion = productVersion,
            FileVersion = fileVersion
        };
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var plusIndex = trimmed.IndexOf('+');
        return plusIndex > 0 ? trimmed[..plusIndex] : trimmed;
    }
}
