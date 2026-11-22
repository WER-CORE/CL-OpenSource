using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CL.Core.Helpers
{
    internal static class AtomicFileWriter
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks =
            new(StringComparer.OrdinalIgnoreCase);

        private static SemaphoreSlim GetPathLock(string targetPath)
        {
            string fullPath = Path.GetFullPath(targetPath);
            return PathLocks.GetOrAdd(fullPath, _ => new SemaphoreSlim(1, 1));
        }

        public static async Task WriteAllTextAtomicAsync(
            string targetPath,
            string content,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Target path cannot be empty.", nameof(targetPath));

            token.ThrowIfCancellationRequested();

            string tempPath = CreateTempPath(targetPath);
            var pathLock = GetPathLock(targetPath);
            try
            {
                await File.WriteAllTextAsync(tempPath, content, token).ConfigureAwait(false);
                await pathLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    File.Move(tempPath, targetPath, overwrite: true);
                }
                finally
                {
                    pathLock.Release();
                }
            }
            finally
            {
                TryDeleteFile(tempPath);
            }
        }

        public static async Task WriteAllBytesAtomicAsync(
            string targetPath,
            byte[] data,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Target path cannot be empty.", nameof(targetPath));

            token.ThrowIfCancellationRequested();

            string tempPath = CreateTempPath(targetPath);
            var pathLock = GetPathLock(targetPath);
            try
            {
                await File.WriteAllBytesAsync(tempPath, data, token).ConfigureAwait(false);
                await pathLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    File.Move(tempPath, targetPath, overwrite: true);
                }
                finally
                {
                    pathLock.Release();
                }
            }
            finally
            {
                TryDeleteFile(tempPath);
            }
        }

        public static void WriteAllTextAtomic(string targetPath, string content)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Target path cannot be empty.", nameof(targetPath));

            string tempPath = CreateTempPath(targetPath);
            var pathLock = GetPathLock(targetPath);
            try
            {
                File.WriteAllText(tempPath, content);
                pathLock.Wait();
                try
                {
                    File.Move(tempPath, targetPath, overwrite: true);
                }
                finally
                {
                    pathLock.Release();
                }
            }
            finally
            {
                TryDeleteFile(tempPath);
            }
        }

        private static string CreateTempPath(string targetPath)
        {
            string? dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string fileName = Path.GetFileName(targetPath);
            string baseDir = string.IsNullOrEmpty(dir) ? Directory.GetCurrentDirectory() : dir;
            return Path.Combine(baseDir, $"{fileName}.{Guid.NewGuid():N}.tmp");
        }

        private static void TryDeleteFile(string path)
        {
            if (!File.Exists(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
