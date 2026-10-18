using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace CL.Core.Services;

public class SecretConfiguration
{
    public string LauncherSecret { get; set; } = string.Empty;
    public string CurseForgeKeyUrl { get; set; } = string.Empty;
    public string CurseForgeDirectApiKey { get; set; } = string.Empty;
    public string GeminiApiUrl { get; set; } = string.Empty;
    public string GeminiModelUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite-preview:generateContent";
    public string GeminiDirectApiKey { get; set; } = string.Empty;
    public string SentryDsn { get; set; } = string.Empty;
    public string AzureClientId { get; set; } = "a28c1b8e-0078-436d-b296-15536f44ac81";
    public string DiscordClientId { get; set; } = "1210664596289884200";
}

public static class SecretsProvider
{
    public const string UserSecretsId = "CLegendary-Launcher-Secrets-5f3a9e2d";

    private static readonly Lazy<SecretConfiguration> _config = new(LoadConfiguration);

    public static string LauncherSecret => _config.Value.LauncherSecret;
    public static string CurseForgeKeyUrl => _config.Value.CurseForgeKeyUrl;
    public static string CurseForgeDirectApiKey => _config.Value.CurseForgeDirectApiKey;
    public static string GeminiApiUrl => _config.Value.GeminiApiUrl;
    public static string GeminiModelUrl => _config.Value.GeminiModelUrl;
    public static string GeminiDirectApiKey => _config.Value.GeminiDirectApiKey;
    public static string SentryDsn => _config.Value.SentryDsn;
    public static string AzureClientId => _config.Value.AzureClientId;
    public static string DiscordClientId => _config.Value.DiscordClientId;

    public static bool HasLauncherSecret => !string.IsNullOrWhiteSpace(_config.Value.LauncherSecret);
    public static bool HasCurseForgeAccess => !string.IsNullOrWhiteSpace(_config.Value.CurseForgeDirectApiKey) || !string.IsNullOrWhiteSpace(_config.Value.CurseForgeKeyUrl);
    public static bool HasGeminiAccess => !string.IsNullOrWhiteSpace(_config.Value.GeminiDirectApiKey) || !string.IsNullOrWhiteSpace(_config.Value.GeminiApiUrl);
    public static bool HasSentryDsn => !string.IsNullOrWhiteSpace(_config.Value.SentryDsn);

    private static SecretConfiguration LoadConfiguration()
    {
        var config = new SecretConfiguration();

        string userSecretsPath = GetUserSecretsPath(UserSecretsId);
        if (File.Exists(userSecretsPath))
        {
            ApplyJsonFile(userSecretsPath, config);
        }

        string localSecretsBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secrets.local.json");
        if (File.Exists(localSecretsBase))
        {
            ApplyJsonFile(localSecretsBase, config);
        }

        try
        {
            string localSecretsAppData = Path.Combine(PlatformPaths.AppDataDirectory(), "secrets.local.json");
            if (File.Exists(localSecretsAppData))
            {
                ApplyJsonFile(localSecretsAppData, config);
            }
        }
        catch { }

        ApplyEnvironmentVariables(config);

        return config;
    }

    private static void ApplyJsonFile(string path, SecretConfiguration config)
    {
        try
        {
            string json = File.ReadAllText(path);
            var root = JObject.Parse(json);

            var secretsObj = (root["Secrets"] as JObject) ?? root;

            if (secretsObj["LauncherSecret"] is JValue ls && ls.Value != null) config.LauncherSecret = ls.ToString();
            if (secretsObj["CurseForgeKeyUrl"] is JValue cfUrl && cfUrl.Value != null) config.CurseForgeKeyUrl = cfUrl.ToString();
            if (secretsObj["CurseForgeDirectApiKey"] is JValue cfKey && cfKey.Value != null) config.CurseForgeDirectApiKey = cfKey.ToString();
            if (secretsObj["GeminiApiUrl"] is JValue gUrl && gUrl.Value != null) config.GeminiApiUrl = gUrl.ToString();
            if (secretsObj["GeminiModelUrl"] is JValue gmUrl && gmUrl.Value != null) config.GeminiModelUrl = gmUrl.ToString();
            if (secretsObj["GeminiDirectApiKey"] is JValue gKey && gKey.Value != null) config.GeminiDirectApiKey = gKey.ToString();
            if (secretsObj["SentryDsn"] is JValue sDsn && sDsn.Value != null) config.SentryDsn = sDsn.ToString();
            if (secretsObj["AzureClientId"] is JValue aId && aId.Value != null) config.AzureClientId = aId.ToString();
            if (secretsObj["DiscordClientId"] is JValue dId && dId.Value != null) config.DiscordClientId = dId.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SecretsProvider] Failed to parse secrets from {path}: {ex.Message}");
        }
    }

    private static void ApplyEnvironmentVariables(SecretConfiguration config)
    {
        string? envLauncherSecret = Environment.GetEnvironmentVariable("CL_LAUNCHER_SECRET");
        if (!string.IsNullOrWhiteSpace(envLauncherSecret)) config.LauncherSecret = envLauncherSecret;

        string? envCfUrl = Environment.GetEnvironmentVariable("CL_CURSEFORGE_KEY_URL");
        if (!string.IsNullOrWhiteSpace(envCfUrl)) config.CurseForgeKeyUrl = envCfUrl;

        string? envCfKey = Environment.GetEnvironmentVariable("CL_CURSEFORGE_DIRECT_KEY");
        if (!string.IsNullOrWhiteSpace(envCfKey)) config.CurseForgeDirectApiKey = envCfKey;

        string? envGeminiUrl = Environment.GetEnvironmentVariable("CL_GEMINI_API_URL");
        if (!string.IsNullOrWhiteSpace(envGeminiUrl)) config.GeminiApiUrl = envGeminiUrl;

        string? envGeminiKey = Environment.GetEnvironmentVariable("CL_GEMINI_DIRECT_KEY");
        if (!string.IsNullOrWhiteSpace(envGeminiKey)) config.GeminiDirectApiKey = envGeminiKey;

        string? envSentry = Environment.GetEnvironmentVariable("CL_SENTRY_DSN");
        if (!string.IsNullOrWhiteSpace(envSentry)) config.SentryDsn = envSentry;

        string? envAzure = Environment.GetEnvironmentVariable("CL_AZURE_CLIENT_ID");
        if (!string.IsNullOrWhiteSpace(envAzure)) config.AzureClientId = envAzure;

        string? envDiscord = Environment.GetEnvironmentVariable("CL_DISCORD_CLIENT_ID");
        if (!string.IsNullOrWhiteSpace(envDiscord)) config.DiscordClientId = envDiscord;
    }

    public static string GetUserSecretsPath(string secretsId)
    {
        if (OperatingSystem.IsWindows())
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Microsoft", "UserSecrets", secretsId, "secrets.json");
        }
        else
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".microsoft", "usersecrets", secretsId, "secrets.json");
        }
    }
}
