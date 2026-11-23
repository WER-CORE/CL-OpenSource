using System;
using CL.Core.Services;
using Sentry;
using Xunit;

namespace CL.Core.Tests;

public class CrashReportManagerTests
{
    [Fact]
    public void ApplySettings_WhenEnabledAtStartup_InvokesEnable_AndWhenDisabled_InvokesDisable()
    {
        int enableCalls = 0;
        int disableCalls = 0;

        CrashReportManager.ApplySettings(true, () => enableCalls++, () => disableCalls++);
        Assert.Equal(1, enableCalls);
        Assert.Equal(0, disableCalls);

        // Simulated restart with saved IsCrashReportingEnabled = true
        CrashReportManager.ApplySettings(true, () => enableCalls++, () => disableCalls++);
        Assert.Equal(2, enableCalls);
        Assert.Equal(0, disableCalls);

        // Simulated toggle off during session or startup with IsCrashReportingEnabled = false
        CrashReportManager.ApplySettings(false, () => enableCalls++, () => disableCalls++);
        Assert.Equal(2, enableCalls);
        Assert.Equal(1, disableCalls);
    }

    [Fact]
    public void Startup_WithCrashReportingDisabled_DoesNotStartSentry()
    {
        CrashReportManager.Disable();
        CrashReportManager.ApplySettings(false);

        Assert.False(SentrySdk.IsEnabled);
    }

    [Fact]
    public void Enable_WithEmptyDsn_AndRepeatedDisableCalls_AreSafeAndDoNotThrow()
    {
        CrashReportManager.Disable();
        CrashReportManager.Enable(dsnOverride: string.Empty);
        CrashReportManager.Enable(dsnOverride: "   ");

        Assert.False(SentrySdk.IsEnabled);

        CrashReportManager.Disable();
        CrashReportManager.Disable();
        Assert.False(SentrySdk.IsEnabled);
    }

    [Fact]
    public void Enable_RepeatedCallsWithNonSendingTestDsn_AreIdempotentAndCanBeDisabled()
    {
        // Use a non-routable local DSN and ensure no events are ever sent to production.
        CrashReportManager.Disable();
        try
        {
            CrashReportManager.Enable(dsnOverride: "https://00000000000000000000000000000000@127.0.0.1/0");
            Assert.True(SentrySdk.IsEnabled);

            // Second Enable() call must be a safe no-op
            CrashReportManager.Enable(dsnOverride: "https://00000000000000000000000000000000@127.0.0.1/0");
            Assert.True(SentrySdk.IsEnabled);

            CrashReportManager.Disable();
            Assert.False(SentrySdk.IsEnabled);
        }
        finally
        {
            CrashReportManager.Disable();
        }
    }

    [Theory]
    [InlineData("Stable", true, "development")]
    [InlineData("Preview", true, "development")]
    [InlineData("Stable", false, "production")]
    [InlineData("Preview", false, "preview")]
    [InlineData("Testing", false, "preview")]
    public void ResolveEnvironment_SeparatesDevelopmentPreviewAndProduction(string channel, bool isDebug, string expected)
    {
        string env = CrashReportManager.ResolveEnvironment(channel, isDebug);
        Assert.Equal(expected, env);
    }

    [Fact]
    public void ResolveRelease_ReturnsCleanAssemblyNameAndVersion()
    {
        string release = CrashReportManager.ResolveRelease(typeof(CrashReportManager).Assembly);
        Assert.StartsWith("CL.Core@", release, StringComparison.Ordinal);
        Assert.DoesNotContain("+", release, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizePiiPaths_MasksWindowsAndUnixUserHomePaths()
    {
        string winMsg = @"The process cannot access the file 'C:\Users\alice\AppData\Roaming\.ClMinecraft\assets\a.dat'";
        string maskedWin = CrashReportManager.SanitizePiiPaths(winMsg, @"C:\Users\alice", "alice");
        Assert.DoesNotContain("alice", maskedWin, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<USER_HOME>", maskedWin, StringComparison.Ordinal);

        string linuxMsg = "Failed to read /home/bob/.local/share/CLMinecraft/UserSaves.json";
        string maskedLinux = CrashReportManager.SanitizePiiPaths(linuxMsg, "/home/bob", "bob");
        Assert.DoesNotContain("bob", maskedLinux, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<USER_HOME>", maskedLinux, StringComparison.Ordinal);
    }
}
