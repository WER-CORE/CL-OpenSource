using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public sealed record LogTailReadResult(
        string Content,
        bool IsTruncated,
        long TotalBytes,
        int ReadBytes);

    public static class LogFileTailReader
    {
        public const int DefaultMaxTailBytes = 512 * 1024;

        public static async Task<LogTailReadResult> ReadTailAsync(
            string filePath,
            int maxBytes = DefaultMaxTailBytes,
            CancellationToken cancellationToken = default,
            bool trimTrailingNewLines = false)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));
            if (maxBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));

            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 16384,
                useAsync: true);

            long totalLength = stream.Length;
            if (totalLength == 0)
            {
                return new LogTailReadResult(string.Empty, false, 0, 0);
            }

            bool isTruncated = totalLength > maxBytes;
            int toRead = (int)Math.Min(totalLength, maxBytes);
            long offset = isTruncated ? totalLength - toRead : 0;

            if (offset > 0)
            {
                stream.Seek(offset, SeekOrigin.Begin);
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(toRead);
            try
            {
                int totalRead = 0;
                while (totalRead < toRead)
                {
                    int read = await stream
                        .ReadAsync(buffer.AsMemory(totalRead, toRead - totalRead), cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }
                    totalRead += read;
                }

                if (totalRead == 0)
                {
                    return new LogTailReadResult(string.Empty, isTruncated, totalLength, 0);
                }

                int start = 0;
                if (isTruncated)
                {
                    int maxContinuationSkip = Math.Min(totalRead, 3);
                    while (start < maxContinuationSkip && (buffer[start] & 0xC0) == 0x80)
                    {
                        start++;
                    }

                    int firstNewLine = Array.IndexOf(buffer, (byte)'\n', start, totalRead - start);
                    if (firstNewLine >= 0 && firstNewLine + 1 < totalRead)
                    {
                        start = firstNewLine + 1;
                    }
                }
                else if (totalRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                {
                    start = 3;
                }

                int end = totalRead;
                if (trimTrailingNewLines)
                {
                    while (end > start && (buffer[end - 1] == (byte)'\n' || buffer[end - 1] == (byte)'\r'))
                    {
                        end--;
                    }
                }

                string text = end > start
                    ? Encoding.UTF8.GetString(buffer, start, end - start)
                    : string.Empty;

                return new LogTailReadResult(text, isTruncated, totalLength, totalRead);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
