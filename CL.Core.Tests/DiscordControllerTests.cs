using System.Threading.Tasks;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests;

public class DiscordControllerTests
{
    [Fact]
    public async Task Initialize_WhenDiscordRpcDisabled_DoesNotThrowAndDoesNotCreateClient()
    {

        SettingsManager.Default.EnableMod_DiscordRPC = false;
        SettingsManager.Default.OfflineModLauncher = false;
        DiscordController.Deinitialize();

        await DiscordController.Initialize("Test State");

        Assert.Null(DiscordController.clientdsc);
    }

    [Fact]
    public async Task Initialize_WhenOfflineModeEnabled_DoesNotThrowAndDoesNotCreateClient()
    {

        SettingsManager.Default.EnableMod_DiscordRPC = true;
        SettingsManager.Default.OfflineModLauncher = true;
        DiscordController.Deinitialize();

        await DiscordController.Initialize("Test State");

        Assert.Null(DiscordController.clientdsc);
    }

    [Fact]
    public async Task UpdatePresence_WhenDisabled_DoesNotThrow()
    {

        SettingsManager.Default.EnableMod_DiscordRPC = false;
        DiscordController.Deinitialize();

        await DiscordController.UpdatePresence("Testing Presence");
        Assert.Null(DiscordController.clientdsc);
    }

    [Fact]
    public void Deinitialize_WhenDisabled_CleansUpProperly()
    {

        SettingsManager.Default.EnableMod_DiscordRPC = false;

        DiscordController.Deinitialize();

        Assert.Null(DiscordController.clientdsc);
    }

    [Fact]
    public void Deinitialize_CalledMultipleTimes_IsIdempotent()
    {

        DiscordController.Deinitialize();
        DiscordController.Deinitialize();
        DiscordController.Deinitialize();
        Assert.Null(DiscordController.clientdsc);
    }
}
