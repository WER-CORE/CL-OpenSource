using System;
using System.Diagnostics;

namespace CL.Core.Services
{
    public sealed class ProcessOutputSubscription : IDisposable
    {
        private readonly object _sync = new();
        private readonly Action<string> _onLine;
        private Process? _process;
        private bool _disposed;

        public ProcessOutputSubscription(Action<string> onLine)
        {
            _onLine = onLine ?? throw new ArgumentNullException(nameof(onLine));
        }

        public bool IsAttached
        {
            get
            {
                lock (_sync)
                {
                    return _process != null && !_disposed;
                }
            }
        }

        public bool IsDisposed
        {
            get
            {
                lock (_sync)
                {
                    return _disposed;
                }
            }
        }

        public void Attach(Process process)
        {
            if (process == null) throw new ArgumentNullException(nameof(process));

            lock (_sync)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ProcessOutputSubscription));

                if (ReferenceEquals(_process, process))
                    return;

                DetachLocked();
                _process = process;
                process.OutputDataReceived += OnDataReceived;
                process.ErrorDataReceived += OnDataReceived;
            }
        }

        public void Detach()
        {
            lock (_sync)
            {
                DetachLocked();
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                DetachLocked();
            }
        }

        private void OnDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            lock (_sync)
            {
                if (_disposed || !ReferenceEquals(_process, sender))
                    return;
            }

            _onLine(e.Data);
        }

        private void DetachLocked()
        {
            if (_process == null)
                return;

            _process.OutputDataReceived -= OnDataReceived;
            _process.ErrorDataReceived -= OnDataReceived;
            _process = null;
        }
    }
}
