using System;
using System.Threading;

namespace CL.Core.Services
{
    public sealed class ProgressWindowSessionCoordinator
    {
        private readonly object _sync = new object();
        private readonly Action<Action> _dispatch;
        private readonly Action<long, CancellationTokenSource?, string> _onOpenWindow;
        private readonly Action<long> _onCloseWindow;
        private readonly ProgressUpdateCoalescer? _coalescer;

        private long _generation;
        private bool _isSessionActive;
        private CancellationTokenSource? _activeOwnerCts;

        private long _uiActiveWindowGen;
        private bool _hasPendingSnapshot;
        private long _pendingSnapshotGen;
        private ProgressSnapshot _pendingSnapshot;

        public ProgressWindowSessionCoordinator(
            Action<Action> dispatch,
            Action<long> onOpenWindow,
            Action<long> onCloseWindow,
            ProgressUpdateCoalescer? coalescer = null)
            : this(
                dispatch,
                (gen, _, _) => (onOpenWindow ?? throw new ArgumentNullException(nameof(onOpenWindow)))(gen),
                onCloseWindow,
                coalescer)
        {
        }

        public ProgressWindowSessionCoordinator(
            Action<Action> dispatch,
            Action<long, CancellationTokenSource?, string> onOpenWindow,
            Action<long> onCloseWindow,
            ProgressUpdateCoalescer? coalescer = null)
        {
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            _onOpenWindow = onOpenWindow ?? throw new ArgumentNullException(nameof(onOpenWindow));
            _onCloseWindow = onCloseWindow ?? throw new ArgumentNullException(nameof(onCloseWindow));
            _coalescer = coalescer;
        }

        public bool IsSessionActive
        {
            get
            {
                lock (_sync)
                {
                    return _isSessionActive;
                }
            }
        }

        public long CurrentGeneration
        {
            get
            {
                lock (_sync)
                {
                    return _generation;
                }
            }
        }

        public long ActiveUiWindowGeneration => _uiActiveWindowGen;

        public long BeginSession(CancellationTokenSource? ownerCts = null, string title = "")
        {
            long sessionGen;
            string capturedTitle = title ?? string.Empty;

            lock (_sync)
            {
                _generation++;
                sessionGen = _generation;
                _isSessionActive = true;
                _activeOwnerCts = ownerCts;
                _coalescer?.BeginSession();
            }

            _dispatch(() =>
            {
                if (!CanOpenWindowOnUiThread(sessionGen))
                {
                    return;
                }

                _onOpenWindow(sessionGen, ownerCts, capturedTitle);
            });

            return sessionGen;
        }

        public bool CloseSession(CancellationTokenSource? ownerCts = null)
        {
            long closedUpToGen;
            lock (_sync)
            {
                if (ownerCts != null && !ReferenceEquals(_activeOwnerCts, ownerCts))
                {
                    return false;
                }

                if (!_isSessionActive && ownerCts != null)
                {
                    return false;
                }

                _isSessionActive = false;
                _activeOwnerCts = null;
                closedUpToGen = _generation;
                _generation++;
                _coalescer?.InvalidateSession();
            }

            _dispatch(() => _onCloseWindow(closedUpToGen));
            return true;
        }

        public bool CloseSessionByGeneration(long sessionGen)
        {
            long closedUpToGen;
            lock (_sync)
            {
                if (!_isSessionActive || sessionGen != _generation)
                {
                    return false;
                }

                _isSessionActive = false;
                _activeOwnerCts = null;
                closedUpToGen = _generation;
                _generation++;
                _coalescer?.InvalidateSession();
            }

            _dispatch(() => _onCloseWindow(closedUpToGen));
            return true;
        }

        public bool CanOpenWindowOnUiThread(long sessionGen)
        {
            lock (_sync)
            {
                return _isSessionActive && sessionGen == _generation;
            }
        }

        public bool IsCurrentGeneration(long sessionGen)
        {
            lock (_sync)
            {
                return _isSessionActive && sessionGen == _generation;
            }
        }

        public void NotifyWindowOpenedOnUiThread(long sessionGen, Action<ProgressSnapshot> applyToCurrentWindow)
        {
            _uiActiveWindowGen = sessionGen;

            if (_hasPendingSnapshot)
            {
                if (_pendingSnapshotGen == sessionGen)
                {
                    var snapshot = _pendingSnapshot;
                    _hasPendingSnapshot = false;
                    applyToCurrentWindow(snapshot);
                }
                else if (_pendingSnapshotGen < sessionGen)
                {
                    _hasPendingSnapshot = false;
                }
            }
        }

        public void NotifyWindowClosedOnUiThread(long closedUpToGen)
        {
            if (_hasPendingSnapshot && _pendingSnapshotGen <= closedUpToGen)
            {
                _hasPendingSnapshot = false;
            }

            if (_uiActiveWindowGen <= closedUpToGen)
            {
                _uiActiveWindowGen = 0;
            }
        }

        public void RouteSnapshotOnUiThread(ProgressSnapshot snapshot, Action<ProgressSnapshot> applyToCurrentWindow)
        {
            long targetGen = snapshot.SessionGeneration;
            if (targetGen <= 0 || !IsCurrentGeneration(targetGen))
            {
                return;
            }

            if (_uiActiveWindowGen != targetGen)
            {
                if (!_hasPendingSnapshot || _pendingSnapshotGen != targetGen)
                {
                    _pendingSnapshot = snapshot;
                    _pendingSnapshotGen = targetGen;
                    _hasPendingSnapshot = true;
                }
                else
                {
                    _pendingSnapshot = new ProgressSnapshot(
                        snapshot.HasFileProgress || _pendingSnapshot.HasFileProgress,
                        snapshot.HasFileProgress ? snapshot.FileProgress : _pendingSnapshot.FileProgress,
                        snapshot.HasVersionProgress || _pendingSnapshot.HasVersionProgress,
                        snapshot.HasVersionProgress ? snapshot.VersionProgress : _pendingSnapshot.VersionProgress,
                        snapshot.HasVersionProgress ? snapshot.VersionLabel : _pendingSnapshot.VersionLabel,
                        snapshot.HasFileTaskProgress || _pendingSnapshot.HasFileTaskProgress,
                        snapshot.HasFileTaskProgress ? snapshot.TotalFiles : _pendingSnapshot.TotalFiles,
                        snapshot.HasFileTaskProgress ? snapshot.DownloadedFiles : _pendingSnapshot.DownloadedFiles,
                        snapshot.HasFileTaskProgress ? snapshot.FileName : _pendingSnapshot.FileName,
                        targetGen);
                }
                return;
            }

            applyToCurrentWindow(snapshot);
        }
    }
}
