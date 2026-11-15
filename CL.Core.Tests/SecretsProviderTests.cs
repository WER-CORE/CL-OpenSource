using System.IO;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests;

public class SecretsProviderTests
{
    [Fact]
    public void SecretsProvider_HasValidSecretsId()
    {
        Assert.Equal("CLegendary-Launcher-Secrets-5f3a9e2d", SecretsProvider.UserSecretsId);
    }

    [Fact]
    public void SecretsProvider_GetUserSecretsPath_ReturnsExpectedPath()
    {
        string path = SecretsProvider.GetUserSecretsPath("test-id");
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.Contains("test-id", path);
        Assert.EndsWith("secrets.json", path);
    }

    [Fact]
    public void SecretsProvider_LoadsUserSecrets_WhenFileExists()
    {

        string secretsPath = SecretsProvider.GetUserSecretsPath(SecretsProvider.UserSecretsId);
        if (File.Exists(secretsPath))
        {
            Assert.True(SecretsProvider.HasLauncherSecret);
            Assert.False(string.IsNullOrWhiteSpace(SecretsProvider.LauncherSecret));
            Assert.False(string.IsNullOrWhiteSpace(Secrets.LauncherSecret));
            Assert.False(string.IsNullOrWhiteSpace(Secrets.CurseForgeKey));
        }
    }

    [Fact]
    public void Secrets_PublicEndpoints_AreNeverNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(Secrets.CLIENT_ID_AZURE));
        Assert.False(string.IsNullOrWhiteSpace(Secrets.LocalizationURL));
        Assert.False(string.IsNullOrWhiteSpace(Secrets.FundraiserURL));
        Assert.False(string.IsNullOrWhiteSpace(Secrets.CreditsUrl));
        Assert.False(string.IsNullOrWhiteSpace(Secrets.EulaUrl));
        Assert.False(string.IsNullOrWhiteSpace(Secrets.NewsUrl));
        Assert.False(string.IsNullOrWhiteSpace(Secrets.AuthUrlLittleSkin));
    }
}
