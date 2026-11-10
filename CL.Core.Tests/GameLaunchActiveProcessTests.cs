using System.Diagnostics;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    [Collection("GameLaunchStaticState")]
    public class GameLaunchActiveProcessTests
    {
        [Fact]
        public void ClearActiveProcessState_ResetsStateForTrackedProcess()
        {
            using var process = new Process();
            GameLaunchService.ActiveProcess = process;
            GameLaunchService.ActiveMinecraftVersion = "1.20.1";
            GameLaunchService.ActiveModpackName = "Pack";
            GameLaunchService.ActiveGamePath = "path";

            GameLaunchService.ClearActiveProcessState(process);

            Assert.Null(GameLaunchService.ActiveProcess);
            Assert.Null(GameLaunchService.ActiveMinecraftVersion);
            Assert.Null(GameLaunchService.ActiveModpackName);
            Assert.Null(GameLaunchService.ActiveGamePath);
        }

        [Fact]
        public void ClearActiveProcessState_KeepsStateOfNewerProcess()
        {
            using var oldProcess = new Process();
            using var newProcess = new Process();
            GameLaunchService.ActiveProcess = newProcess;
            GameLaunchService.ActiveMinecraftVersion = "1.21";

            GameLaunchService.ClearActiveProcessState(oldProcess);

            Assert.Same(newProcess, GameLaunchService.ActiveProcess);
            Assert.Equal("1.21", GameLaunchService.ActiveMinecraftVersion);

            GameLaunchService.ClearActiveProcessState(newProcess);
            Assert.Null(GameLaunchService.ActiveProcess);
        }
    }
}
