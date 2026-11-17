using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Velopack.Sources;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class ChannelStrictGithubSource : GithubSource
{
    public bool OnlyPrerelease { get; }

    public ChannelStrictGithubSource(
        string repoUrl,
        string? accessToken,
        bool onlyPrerelease,
        IFileDownloader? downloader = null)
        : base(repoUrl, accessToken, prerelease: onlyPrerelease, downloader: downloader)
    {
        OnlyPrerelease = onlyPrerelease;
    }

    protected override async Task<GithubRelease[]> GetReleases(bool includePrereleases)
    {
        var releases = await base.GetReleases(includePrereleases);
        return FilterReleasesForChannel(releases, OnlyPrerelease);
    }

    public static GithubRelease[] FilterReleasesForChannel(IEnumerable<GithubRelease>? releases, bool onlyPrerelease)
    {
        if (releases == null) return System.Array.Empty<GithubRelease>();
        return releases.Where(r => r != null && r.Prerelease == onlyPrerelease).ToArray();
    }
}
