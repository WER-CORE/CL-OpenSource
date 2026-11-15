using System.Collections.Generic;

namespace CL.Core.Models.P2P;

public class P2PModEntry
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Sha1 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? DownloadUrl { get; set; }
}

public class P2PManifest
{
    public string GameVersion { get; set; } = string.Empty;
    public string LoaderType { get; set; } = string.Empty;
    public string LoaderVersion { get; set; } = string.Empty;
    public List<P2PModEntry> Mods { get; set; } = new();
}
