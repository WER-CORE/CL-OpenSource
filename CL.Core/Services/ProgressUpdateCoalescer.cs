using System;
using System.Threading;

namespace CL.Core.Services
{
    public readonly record struct ProgressSnapshot(
        bool HasFileProgress,
        int FileProgress,
        bool HasVersionProgress,
        int VersionProgress,
        object? VersionLabel,
        bool HasFileTaskProgress,
        int TotalFiles,
        int DownloadedFiles,
        string FileName,
        long SessionGeneration = 0);

    public sealed class ProgressUpdateCoalescer : IDisposable
    {
        public static readonly TimeSpan DefaultMinInterval = TimeSpan.FromMilliseconds(66);

        private readonly object _sync = new object();
        private readonly Action<ProgressSnapshot> _applySnapshot;
        private readonly Action<Action> _dispatch;
        private readonly Action<int, Action>? _customScheduleDelayed;
        private readonly Timer? _delayTimer;
        private readonly Func<long> _clockMs;
        private readonly long _minIntervalMs;

        private long _generation;
        private long _delayToken;
        private long _lastFlushMs;
        private bool _hasFlushedOnce;
        private bool _lastFlushWasTerminal;
        private bool _flushDispatched;
        private bool _delayPending;
        private bool _disposed;

        private bool _hasReportedFileProgress;
        private int _lastFileProgress = -1;
        private bool _pendingHasFileProgress;
        private int _pendingFileProgress;

        private bool _hasReportedVersionProgress;
        private int _lastVersionProgress = -1;
        private object? _currentVersionLabel;
        private bool _pendingHasVersionProgress;
        private int _pendingVersionProgress;
        private object? _pendingVersionLabel;

        private bool _hasReportedFileTask;
        private int _lastTotalFiles = -1;
        private int _lastDownloadedFiles = -1;
        private string _lastFileName = string.Empty;
        private bool _pendingHasFileTask;
        private int _pendingTotalFiles;
        private int _pendingDownloadedFiles;
        private string _pendingFileName = string.Empty;

        public ProgressUpdateCoalescer(
            Action<ProgressSnapshot> applySnapshot,
            Action<Action> dispatch,
            TimeSpan? minInterval = null,
            Func<long>? clockMs = null,
            Action<int, Action>? scheduleDelayed = null)
        {
            _applySnapshot = applySnapshot ?? throw new ArgumentNullException(nameof(applySnapshot));
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            _clockMs = clockMs ?? (() => Environment.TickCount64);
            _customScheduleDelayed = scheduleDelayed;

            long intervalMs = (long)(minInterval ?? DefaultMinInterval).TotalMilliseconds;
            _minIntervalMs = Math.Max(0, intervalMs);

            if (_customScheduleDelayed == null && _minIntervalMs > 0)
            {
                _delayTimer = new Timer(OnBuiltInTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
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

        public void BeginSession()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _generation++;
                ResetStateLocked();
            }

            StopBuiltInTimer();
        }

        public void InvalidateSession()
        {
            lock (_sync)
            {
                _generation++;
                ResetStateLocked();
            }

            StopBuiltInTimer();
        }

        public void ResetPhase()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _hasReportedFileProgress = false;
                _lastFileProgress = -1;
                _hasReportedVersionProgress = false;
                _lastVersionProgress = -1;
                _currentVersionLabel = null;
                _hasReportedFileTask = false;
                _lastTotalFiles = -1;
                _lastDownloadedFiles = -1;
                _lastFileName = string.Empty;
            }
        }

        public void ReportFileProgress(int progress)
        {
            progress = Math.Clamp(progress, 0, 100);
            Action? postAction = null;

            lock (_sync)
            {
                if (_disposed) return;

                bool phaseCompleted = _hasReportedFileTask && _lastTotalFiles > 0 && _lastDownloadedFiles >= _lastTotalFiles;

                if (_hasReportedFileProgress)
                {
                    if (progress == _lastFileProgress)
                    {
                        return;
                    }

                    if (progress < _lastFileProgress)
                    {
                        if (!phaseCompleted && _lastFileProgress == 100 && progress == 0)
                        {
                        }
                        else
                        {
                            return;
                        }
                    }
                }
                else if (phaseCompleted && progress < 100)
                {
                    return;
                }

                _hasReportedFileProgress = true;
                _lastFileProgress = progress;
                _pendingHasFileProgress = true;
                _pendingFileProgress = progress;

                postAction = PrepareScheduleLocked(isImmediate: progress == 100);
            }

            postAction?.Invoke();
        }

        public void ReportVersionProgress(int progress, object? version)
        {
            progress = Math.Clamp(progress, 0, 100);
            Action? postAction = null;

            lock (_sync)
            {
                if (_disposed) return;

                bool phaseChanged = !_hasReportedVersionProgress ||
                                    !Equals(version, _currentVersionLabel) ||
                                    (_lastVersionProgress == 100 && progress == 0);
                if (phaseChanged)
                {
                    _hasReportedVersionProgress = true;
                    _currentVersionLabel = version;
                    _lastVersionProgress = progress;
                }
                else
                {
                    if (progress <= _lastVersionProgress)
                    {
                        return;
                    }

                    _lastVersionProgress = progress;
                }

                _pendingHasVersionProgress = true;
                _pendingVersionProgress = progress;
                _pendingVersionLabel = version;

                postAction = PrepareScheduleLocked(isImmediate: progress == 100);
            }

            postAction?.Invoke();
        }

        public void ReportFileTaskProgress(int totalFiles, int downloadedFiles, string? fileName)
        {
            string normalizedName = fileName ?? string.Empty;
            Action? postAction = null;

            lock (_sync)
            {
                if (_disposed) return;

                if (_hasReportedFileTask && _lastTotalFiles > 0 && totalFiles <= 0)
                {
                    return;
                }

                bool previousCompleted = _lastTotalFiles > 0 && _lastDownloadedFiles >= _lastTotalFiles;
                bool restartedFromZero = downloadedFiles == 0 && _lastDownloadedFiles > 0;
                bool newPhase = _hasReportedFileTask && (
                    restartedFromZero ||
                    (previousCompleted && totalFiles != _lastTotalFiles && downloadedFiles < totalFiles));

                if (newPhase)
                {
                    _hasReportedFileTask = false;
                    _lastDownloadedFiles = -1;
                    _hasReportedFileProgress = false;
                    _lastFileProgress = -1;
                    _hasReportedVersionProgress = false;
                    _lastVersionProgress = -1;
                }

                if (_hasReportedFileTask)
                {
                    if (downloadedFiles < _lastDownloadedFiles)
                    {
                        return;
                    }

                    if (totalFiles == _lastTotalFiles &&
                        downloadedFiles == _lastDownloadedFiles &&
                        string.Equals(normalizedName, _lastFileName, StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                bool isTerminal = totalFiles > 0 && downloadedFiles >= totalFiles;

                if (!isTerminal &&
                    (downloadedFiles > _lastDownloadedFiles ||
                     !string.Equals(normalizedName, _lastFileName, StringComparison.Ordinal)))
                {
                    _hasReportedFileProgress = false;
                    _lastFileProgress = -1;
                }

                _hasReportedFileTask = true;
                _lastTotalFiles = totalFiles;
                _lastDownloadedFiles = downloadedFiles;
                _lastFileName = normalizedName;

                _pendingHasFileTask = true;
                _pendingTotalFiles = totalFiles;
                _pendingDownloadedFiles = downloadedFiles;
                _pendingFileName = normalizedName;

                postAction = PrepareScheduleLocked(isImmediate: isTerminal);
            }

            postAction?.Invoke();
        }

        private Action? PrepareScheduleLocked(bool isImmediate)
        {
            if (_flushDispatched)
            {
                return null;
            }

            long gen = _generation;
            long nowMs = _clockMs();
            long elapsed = _hasFlushedOnce ? nowMs - _lastFlushMs : _minIntervalMs;

            bool allowImmediate = _minIntervalMs == 0 ||
                                  elapsed >= _minIntervalMs ||
                                  (isImmediate && !_lastFlushWasTerminal);

            if (allowImmediate)
            {
                if (_delayPending)
                {
                    _delayPending = false;
                    _delayToken++;
                }
                _flushDispatched = true;
                return () => _dispatch(() => FlushOnUiThread(gen, isImmediate));
            }

            if (_delayPending)
            {
                return null;
            }

            _delayPending = true;
            long token = ++_delayToken;
            int delayMs = (int)Math.Max(1, _minIntervalMs - elapsed);

            if (_customScheduleDelayed != null)
            {
                return () => _customScheduleDelayed(delayMs, () => TriggerDelayedFlush(gen, token));
            }

            return () =>
            {
                try
                {
                    _delayTimer?.Change(delayMs, Timeout.Infinite);
                }
                catch (ObjectDisposedException)
                {
                }
            };
        }

        private void OnBuiltInTimerElapsed(object? state)
        {
            long gen;
            long token;

            lock (_sync)
            {
                if (_disposed || !_delayPending || _flushDispatched || !HasAnyPendingLocked())
                {
                    return;
                }

                if (_hasFlushedOnce)
                {
                    long elapsed = _clockMs() - _lastFlushMs;
                    if (elapsed < _minIntervalMs)
                    {
                        int remainingMs = (int)Math.Max(1, _minIntervalMs - elapsed);
                        try
                        {
                            _delayTimer?.Change(remainingMs, Timeout.Infinite);
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        return;
                    }
                }

                gen = _generation;
                token = _delayToken;
            }

            TriggerDelayedFlush(gen, token);
        }

        private void TriggerDelayedFlush(long gen, long token)
        {
            bool shouldDispatch;

            lock (_sync)
            {
                if (_disposed || gen != _generation || token != _delayToken || !_delayPending || _flushDispatched || !HasAnyPendingLocked())
                {
                    return;
                }

                if (_hasFlushedOnce && (_clockMs() - _lastFlushMs) < _minIntervalMs)
                {
                    return;
                }

                _delayPending = false;
                _flushDispatched = true;
                shouldDispatch = true;
            }

            if (shouldDispatch)
            {
                _dispatch(() => FlushOnUiThread(gen, isTerminalFlush: false));
            }
        }

        private void FlushOnUiThread(long gen, bool isTerminalFlush)
        {
            ProgressSnapshot snapshot;

            lock (_sync)
            {
                if (_disposed || gen != _generation)
                {
                    return;
                }

                _flushDispatched = false;
                _delayPending = false;

                if (!HasAnyPendingLocked())
                {
                    return;
                }

                snapshot = new ProgressSnapshot(
                    _pendingHasFileProgress,
                    _pendingFileProgress,
                    _pendingHasVersionProgress,
                    _pendingVersionProgress,
                    _pendingVersionLabel,
                    _pendingHasFileTask,
                    _pendingTotalFiles,
                    _pendingDownloadedFiles,
                    _pendingFileName,
                    gen);

                _pendingHasFileProgress = false;
                _pendingHasVersionProgress = false;
                _pendingHasFileTask = false;
                _lastFlushMs = _clockMs();
                _hasFlushedOnce = true;
                _lastFlushWasTerminal = isTerminalFlush;
            }

            _applySnapshot(snapshot);
        }

        private bool HasAnyPendingLocked()
        {
            return _pendingHasFileProgress || _pendingHasVersionProgress || _pendingHasFileTask;
        }

        private void ResetStateLocked()
        {
            _flushDispatched = false;
            _delayPending = false;
            _hasFlushedOnce = false;
            _lastFlushWasTerminal = false;
            _lastFlushMs = 0;

            _pendingHasFileProgress = false;
            _pendingHasVersionProgress = false;
            _pendingHasFileTask = false;

            _hasReportedFileProgress = false;
            _lastFileProgress = -1;
            _hasReportedVersionProgress = false;
            _lastVersionProgress = -1;
            _currentVersionLabel = null;
            _hasReportedFileTask = false;
            _lastTotalFiles = -1;
            _lastDownloadedFiles = -1;
            _lastFileName = string.Empty;
        }

        private void StopBuiltInTimer()
        {
            try
            {
                _delayTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                _generation++;
                ResetStateLocked();
            }

            _delayTimer?.Dispose();
        }
    }
}
