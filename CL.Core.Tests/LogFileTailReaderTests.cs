using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    public class LogFileTailReaderTests : IDisposable
    {
        private readonly string _tempDir;

        public LogFileTailReaderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "cl_tail_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
            }
        }

        [Fact]
        public async Task ReadTailAsync_EmptyFile_ReturnsEmptyNotTruncated()
        {
            string file = Path.Combine(_tempDir, "empty.log");
            await File.WriteAllTextAsync(file, string.Empty);

            var result = await LogFileTailReader.ReadTailAsync(file);

            Assert.False(result.IsTruncated);
            Assert.Equal(0, result.TotalBytes);
            Assert.Equal(string.Empty, result.Content);
        }

        [Fact]
        public async Task ReadTailAsync_SmallFileWithBom_ReadsFullContentWithoutBom()
        {
            string file = Path.Combine(_tempDir, "small.log");
            string expected = "рядок 1\nрядок 2 🎮\n";
            await File.WriteAllTextAsync(file, expected, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var result = await LogFileTailReader.ReadTailAsync(file);

            Assert.False(result.IsTruncated);
            Assert.Equal(expected, result.Content);
        }

        [Fact]
        public async Task ReadTailAsync_LargeFile_ReadsOnlyTailAndAlignsToLineBoundary()
        {
            string file = Path.Combine(_tempDir, "large.log");
            var sb = new StringBuilder();
            for (int i = 0; i < 20_000; i++)
            {
                sb.Append($"[{i:D5}] Minecraft log line payload for tail test\n");
            }

            await File.WriteAllTextAsync(file, sb.ToString(), new UTF8Encoding(false));

            var result = await LogFileTailReader.ReadTailAsync(file);

            Assert.True(result.IsTruncated);
            Assert.True(result.TotalBytes > LogFileTailReader.DefaultMaxTailBytes);
            Assert.Equal(LogFileTailReader.DefaultMaxTailBytes, result.ReadBytes);
            Assert.StartsWith("[", result.Content);
            Assert.EndsWith("[19999] Minecraft log line payload for tail test\n", result.Content);
            Assert.DoesNotContain("[00000]", result.Content);
        }

        [Fact]
        public async Task ReadTailAsync_MultibyteUtf8CutBoundary_DecodesValidUtf8WithoutReplacementChar()
        {
            string file = Path.Combine(_tempDir, "utf8.log");
            var sb = new StringBuilder();
            for (int i = 0; i < 2_000; i++)
            {
                sb.Append($"[{i:D4}] Повідомлення з кирилицею та емодзі 🎮🔥\n");
            }

            await File.WriteAllTextAsync(file, sb.ToString(), new UTF8Encoding(false));

            for (int maxBytes = 1021; maxBytes <= 1027; maxBytes++)
            {
                var result = await LogFileTailReader.ReadTailAsync(file, maxBytes: maxBytes);

                Assert.True(result.IsTruncated);
                Assert.DoesNotContain('\uFFFD', result.Content);
                Assert.EndsWith("[1999] Повідомлення з кирилицею та емодзі 🎮🔥\n", result.Content);
            }
        }

        [Fact]
        public async Task ReadTailAsync_WhileFileIsOpenForWrite_Succeeds()
        {
            string file = Path.Combine(_tempDir, "locked_write.log");
            using var writerStream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            byte[] payload = Encoding.UTF8.GetBytes("live line 1\nlive line 2\n");
            await writerStream.WriteAsync(payload);
            await writerStream.FlushAsync();

            var result = await LogFileTailReader.ReadTailAsync(file);

            Assert.False(result.IsTruncated);
            Assert.Equal("live line 1\nlive line 2\n", result.Content);
        }

        [Fact]
        public async Task ReadTailAsync_MissingFile_ThrowsFileNotFoundException()
        {
            string file = Path.Combine(_tempDir, "missing.log");
            await Assert.ThrowsAsync<FileNotFoundException>(() => LogFileTailReader.ReadTailAsync(file));
        }

        [Fact]
        public async Task ReadTailAsync_CrlfAndUtf8WithTrimTrailingNewLines_TrimsTrailingEolWithoutAllocatingExtraCopy()
        {
            string file = Path.Combine(_tempDir, "crlf_utf8.log");
            var sb = new StringBuilder();
            for (int i = 0; i < 500; i++)
            {
                sb.Append($"[{i:D4}] Рядок з CRLF та емодзі 🚀\r\n");
            }

            await File.WriteAllTextAsync(file, sb.ToString(), new UTF8Encoding(false));

            var result = await LogFileTailReader.ReadTailAsync(file, maxBytes: 1024, trimTrailingNewLines: true);

            Assert.True(result.IsTruncated);
            Assert.DoesNotContain('\uFFFD', result.Content);
            Assert.EndsWith("[0499] Рядок з CRLF та емодзі 🚀", result.Content);
            Assert.False(result.Content.EndsWith("\n") || result.Content.EndsWith("\r"));
        }
    }
}
