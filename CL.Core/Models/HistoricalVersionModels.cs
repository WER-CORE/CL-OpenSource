using System;
using System.Collections.Generic;

namespace CL.Core.Models
{
    public enum VersionCatalogSource
    {
        Mojang,
        BetaCraft,
        Local
    }

    public enum MinecraftEra
    {
        Release,
        Snapshot,
        HistoricalSnapshot,
        Beta,
        Alpha,
        Infdev,
        Indev,
        Classic,
        PreClassic,
        Unknown
    }

    public sealed record CatalogVersionEntry(
        string Id,
        string RawType,
        string NormalizedFilterType,
        MinecraftEra Era,
        VersionCatalogSource Source,
        string? MetadataUrl = null,
        string? Sha1 = null,
        DateTimeOffset? ReleaseTime = null,
        bool HasConflict = false,
        string? ConflictNote = null);

    public sealed record CatalogMergeResult(
        IReadOnlyList<CatalogVersionEntry> Versions,
        IReadOnlyList<string> ConflictLogs,
        bool MojangLoaded,
        bool BetaCraftLoaded,
        string? BetaCraftError = null);
}
