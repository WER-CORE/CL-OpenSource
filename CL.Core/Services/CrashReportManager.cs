using System;
using System.Reflection;
using Sentry;

namespace CL.Core.Services
{
    public static class CrashReportManager
    {
        public static void ApplySettings(bool isCrashReportingEnabled) =>
            ApplySettings(isCrashReportingEnabled, () => Enable(), Disable);

        internal static void ApplySettings(bool isCrashReportingEnabled, Action enableAction, Action disableAction)
        {
            if (isCrashReportingEnabled)
            {
                enableAction();
            }
            else
            {
                disableAction();
            }
        }

        public static void Enable(string? dsnOverride = null)
        {
            string dsn = dsnOverride ?? Secrets.DNS_KEY_AutoReport;
            if (SentrySdk.IsEnabled || string.IsNullOrWhiteSpace(dsn)) return;

            SentrySdk.Init(o =>
            {
                o.Dsn = dsn;
                o.Debug = false;
                o.SendDefaultPii = false;
                o.Release = ResolveRelease();
                o.Environment = ResolveEnvironment();

                o.SetBeforeSend(sentryEvent =>
                {
                    if (sentryEvent.Exception != null)
                    {
                        string errorString = sentryEvent.Exception.ToString();
                        if (errorString.Contains("System.Net.Sockets.SocketException", StringComparison.Ordinal) ||
                            errorString.Contains("A connection attempt failed", StringComparison.Ordinal))
                        {
                            return null;
                        }
                    }

                    sentryEvent.User = new SentryUser
                    {
                        Id = "Anonymous",
                        IpAddress = "0.0.0.0"
                    };

                    if (sentryEvent.SentryExceptions != null)
                    {
                        foreach (var ex in sentryEvent.SentryExceptions)
                        {
                            if (!string.IsNullOrEmpty(ex.Value))
                            {
                                ex.Value = SanitizePiiPaths(ex.Value);
                            }
                        }
                    }

                    if (sentryEvent.Message != null)
                    {
                        if (!string.IsNullOrEmpty(sentryEvent.Message.Formatted))
                            sentryEvent.Message.Formatted = SanitizePiiPaths(sentryEvent.Message.Formatted);
                        if (!string.IsNullOrEmpty(sentryEvent.Message.Message))
                            sentryEvent.Message.Message = SanitizePiiPaths(sentryEvent.Message.Message);
                    }

                    var keepOs = sentryEvent.Contexts.OperatingSystem;
                    var keepRuntime = sentryEvent.Contexts.Runtime;
                    var keepApp = sentryEvent.Contexts.App;

                    sentryEvent.Contexts.Clear();

                    if (keepOs != null) sentryEvent.Contexts["os"] = keepOs;
                    if (keepRuntime != null) sentryEvent.Contexts["runtime"] = keepRuntime;
                    if (keepApp != null) sentryEvent.Contexts["app"] = keepApp;

                    return sentryEvent;
                });
            });
        }

        public static void Disable()
        {
            if (SentrySdk.IsEnabled)
            {
                SentrySdk.Close();
            }
        }

        public static string ResolveEnvironment(string? updateChannel = null, bool? isDebugBuild = null)
        {
            bool debug = isDebugBuild ??
#if DEBUG
                true;
#else
                false;
#endif
            if (debug)
                return "development";

            string channel = updateChannel ?? SettingsManager.Default?.UpdateChannel ?? "Stable";
            if (string.Equals(channel, "Preview", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(channel, "Testing", StringComparison.OrdinalIgnoreCase))
            {
                return "preview";
            }

            return "production";
        }

        public static string ResolveRelease(Assembly? targetAssembly = null)
        {
            var assembly = targetAssembly ?? Assembly.GetEntryAssembly() ?? typeof(CrashReportManager).Assembly;
            string name = assembly.GetName().Name ?? "CL.Avalonia";
            var infoVer = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                string cleanVer = infoVer.Split('+')[0];
                return $"{name}@{cleanVer}";
            }

            var ver = assembly.GetName().Version;
            return ver != null ? $"{name}@{ver.ToString(3)}" : $"{name}@1.1.0";
        }

        public static string SanitizePiiPaths(string input, string? userProfileOverride = null, string? userNameOverride = null)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string result = input;
            string userProfile = userProfileOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                result = result.Replace(userProfile, "<USER_HOME>", StringComparison.OrdinalIgnoreCase);
            }

            string userName = userNameOverride ?? Environment.UserName;
            if (!string.IsNullOrWhiteSpace(userName) && userName.Length >= 2)
            {
                result = result.Replace($"\\Users\\{userName}\\", "\\Users\\<USER>\\", StringComparison.OrdinalIgnoreCase);
                result = result.Replace($"/home/{userName}/", "/home/<USER>/", StringComparison.Ordinal);
                result = result.Replace($"/Users/{userName}/", "/Users/<USER>/", StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
