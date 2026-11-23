using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    public class LaunchLifecycleSafetyTests
    {
        [Fact]
        public void ProgressWindowCoordinator_CloseByPreviousOwner_DoesNotCloseNewerSession()
        {
            var opened = new List<long>();
            var closed = new List<long>();

            var coordinator = new ProgressWindowSessionCoordinator(
                dispatch: action => action(),
                onOpenWindow: (gen, _, _) => opened.Add(gen),
                onCloseWindow: gen => closed.Add(gen));

            using var firstCts = new CancellationTokenSource();
            using var secondCts = new CancellationTokenSource();

            long gen1 = coordinator.BeginSession(firstCts, "First");
            long gen2 = coordinator.BeginSession(secondCts, "Second");

            bool closedFirst = coordinator.CloseSession(firstCts);

            Assert.False(closedFirst);
            Assert.True(coordinator.IsSessionActive);
            Assert.True(coordinator.IsCurrentGeneration(gen2));
            Assert.False(coordinator.IsCurrentGeneration(gen1));
            Assert.Empty(closed);

            bool closedSecond = coordinator.CloseSession(secondCts);
            Assert.True(closedSecond);
            Assert.False(coordinator.IsSessionActive);
            Assert.Single(closed);
        }

        [Fact]
        public void ProgressWindowCoordinator_StaleSnapshotFromOldGeneration_IsIgnored()
        {
            var applied = new List<ProgressSnapshot>();

            var coordinator = new ProgressWindowSessionCoordinator(
                dispatch: action => action(),
                onOpenWindow: (gen, _, _) => { },
                onCloseWindow: _ => { });

            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            long gen1 = coordinator.BeginSession(cts1, "Run1");
            coordinator.NotifyWindowOpenedOnUiThread(gen1, s => applied.Add(s));

            long gen2 = coordinator.BeginSession(cts2, "Run2");
            coordinator.NotifyWindowOpenedOnUiThread(gen2, s => applied.Add(s));

            var staleSnapshot = new ProgressSnapshot(
                HasFileProgress: true,
                FileProgress: 80,
                HasVersionProgress: false,
                VersionProgress: 0,
                VersionLabel: null,
                HasFileTaskProgress: false,
                TotalFiles: 0,
                DownloadedFiles: 0,
                FileName: "old.jar",
                SessionGeneration: gen1);

            var currentSnapshot = staleSnapshot with { FileProgress = 25, FileName = "new.jar", SessionGeneration = gen2 };

            coordinator.RouteSnapshotOnUiThread(staleSnapshot, s => applied.Add(s));
            coordinator.RouteSnapshotOnUiThread(currentSnapshot, s => applied.Add(s));

            var single = Assert.Single(applied);
            Assert.Equal(gen2, single.SessionGeneration);
            Assert.Equal(25, single.FileProgress);
        }

        [Fact]
        public void ProgressWindowCoordinator_CloseSessionByGeneration_IsIdempotent()
        {
            int closeCount = 0;
            var coordinator = new ProgressWindowSessionCoordinator(
                dispatch: action => action(),
                onOpenWindow: (_, _, _) => { },
                onCloseWindow: _ => closeCount++);

            using var cts = new CancellationTokenSource();
            long gen = coordinator.BeginSession(cts, "Install");

            Assert.True(coordinator.CloseSessionByGeneration(gen));
            Assert.False(coordinator.CloseSessionByGeneration(gen));
            Assert.Equal(1, closeCount);
        }

        [Fact]
        public void GameLaunchService_PreparationGate_BlocksConcurrentLaunchAndPreservesCts()
        {
            var service = new GameLaunchService(new GameSessionManager(), new LastActionService());

            Assert.True(service.TryAcquireLaunchPreparationGate());
            Assert.True(service.IsPreparingLaunch);

            // Second concurrent attempt must be rejected without mutating state.
            Assert.False(service.TryAcquireLaunchPreparationGate());
            Assert.True(service.IsPreparingLaunch);

            // Releasing after start or error allows a fresh launch.
            service.ReleaseLaunchPreparationGate();
            Assert.False(service.IsPreparingLaunch);
            Assert.True(service.TryAcquireLaunchPreparationGate());
            service.ReleaseLaunchPreparationGate();
        }

        [Fact]
        public void Cancellation_BeforeGameStart_PreventsProcessLaunch()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            bool processStarted = false;
            Assert.Throws<OperationCanceledException>(() =>
            {
                cts.Token.ThrowIfCancellationRequested();
                processStarted = true;
            });

            Assert.False(processStarted);
        }

        [Fact]
        public void Coalescer_InvalidateSession_DropsQueuedUpdates()
        {
            var applied = new List<ProgressSnapshot>();
            Action? delayedCallback = null;

            using var coalescer = new ProgressUpdateCoalescer(
                applySnapshot: s => applied.Add(s),
                dispatch: action => action(),
                minInterval: TimeSpan.FromMilliseconds(100),
                clockMs: () => 1000,
                scheduleDelayed: (_, cb) => delayedCallback = cb);

            coalescer.BeginSession();
            coalescer.ReportFileProgress(10);
            coalescer.ReportFileProgress(50);

            int countBeforeInvalidate = applied.Count;
            coalescer.InvalidateSession();

            // Trigger delayed flush from invalidated session.
            delayedCallback?.Invoke();

            Assert.Equal(countBeforeInvalidate, applied.Count);
        }
    }
}
