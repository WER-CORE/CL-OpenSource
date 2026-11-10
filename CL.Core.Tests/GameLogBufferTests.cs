using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    public class GameLogBufferTests
    {
        private static string Line(int i) => $"[{i:D6}] повідомлення {i}";

        [Fact]
        public void Append_FirstCallRequestsFlush_NextCallsDoNot_UntilDisplayTaken()
        {
            var buffer = new GameLogBuffer();

            Assert.True(buffer.Append("a"));
            Assert.False(buffer.Append("b"));
            Assert.True(buffer.TryTakeDisplayText(out _));
            Assert.True(buffer.Append("c"));
        }

        [Fact]
        public void TryTakeDisplayText_PreservesOrder()
        {
            var buffer = new GameLogBuffer();
            for (int i = 0; i < 50; i++) buffer.Append(Line(i));

            Assert.True(buffer.TryTakeDisplayText(out string text));

            var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(Enumerable.Range(0, 50).Select(Line), lines);
        }

        [Fact]
        public void TryTakeDisplayText_WithoutPending_ReturnsFalse()
        {
            var buffer = new GameLogBuffer();
            buffer.Append("x");
            buffer.TryTakeDisplayText(out _);

            Assert.False(buffer.TryTakeDisplayText(out string text));
            Assert.Contains("x", text);
        }

        [Fact]
        public void Display_IsBoundedAndKeepsNewestLines()
        {
            var buffer = new GameLogBuffer(maxDisplayChars: 2_000);
            string text = string.Empty;

            for (int i = 0; i < 2_000; i++)
            {
                buffer.Append(Line(i));
                if (i % 7 == 0) buffer.TryTakeDisplayText(out text);
            }
            buffer.TryTakeDisplayText(out text);

            Assert.True(text.Length <= 2_000);
            Assert.EndsWith(Line(1_999) + Environment.NewLine, text);
        }

        [Fact]
        public void Display_TrimsAtLineBoundary()
        {
            var buffer = new GameLogBuffer(maxDisplayChars: 1_000);
            for (int i = 0; i < 500; i++) buffer.Append(Line(i));

            buffer.TryTakeDisplayText(out string text);

            Assert.True(text.Length <= 1_000);
            Assert.StartsWith("[", text);
            var first = text.Split(Environment.NewLine)[0];
            Assert.Matches(@"^\[\d{6}\] повідомлення \d+$", first);
        }

        [Fact]
        public void Display_SingleMessageLargerThanLimit_KeepsTail()
        {
            var buffer = new GameLogBuffer(maxDisplayChars: 1_000);
            string huge = new string('a', 5_000) + "КІНЕЦЬ";

            buffer.Append(huge);
            buffer.TryTakeDisplayText(out string text);

            Assert.True(text.Length <= 1_000);
            Assert.Contains("КІНЕЦЬ", text);
        }

        [Fact]
        public void Display_DoesNotSplitSurrogatePairs()
        {
            var buffer = new GameLogBuffer(maxDisplayChars: 101);
            string emojiLine = string.Concat(Enumerable.Repeat("😀", 400));

            for (int offset = 0; offset < 4; offset++)
            {
                buffer.Clear();
                buffer.Append(new string('x', offset) + emojiLine);
                buffer.TryTakeDisplayText(out string text);

                Assert.False(char.IsLowSurrogate(text[0]));
                _ = new UTF32Encoding(false, false, true).GetBytes(text);
            }
        }

        [Fact]
        public void Display_KeepsMultilineStackTraceIntact()
        {
            var buffer = new GameLogBuffer();
            string trace = string.Join(Environment.NewLine,
                "java.lang.NullPointerException: Не вдалося ініціалізувати 🎮",
                "\tat net.minecraft.client.Main.main(Main.java:1)",
                "Caused by: java.io.IOException");

            buffer.Append("start");
            buffer.Append(trace);
            buffer.TryTakeDisplayText(out string text);

            Assert.Contains(trace, text);
            Assert.Contains(trace, buffer.GetFullText());
        }

        [Fact]
        public void FullText_KeepsEverythingEvenWhenDisplayIsTrimmed()
        {
            var buffer = new GameLogBuffer(maxDisplayChars: 500);
            for (int i = 0; i < 3_000; i++)
            {
                buffer.Append(Line(i));
                if (i % 50 == 0) buffer.TryTakeDisplayText(out _);
            }
            buffer.TryTakeDisplayText(out string display);

            string full = buffer.GetFullText();
            var lines = full.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.True(display.Length <= 500);
            Assert.Equal(Enumerable.Range(0, 3_000).Select(Line), lines);
        }

        [Fact]
        public void FullText_IncludesMessagesNotYetFlushedToDisplay()
        {
            var buffer = new GameLogBuffer();
            buffer.Append("ще не показано");

            Assert.Contains("ще не показано", buffer.GetFullText());
            Assert.True(buffer.HasContent);
        }

        [Fact]
        public void HasContent_IgnoresWhitespaceOnlyMessages()
        {
            var buffer = new GameLogBuffer();
            buffer.Append("   ");
            buffer.Append(string.Empty);

            Assert.False(buffer.HasContent);

            buffer.Append("x");
            Assert.True(buffer.HasContent);
        }

        [Fact]
        public void Clear_ResetsAllState()
        {
            var buffer = new GameLogBuffer();
            buffer.Append("a");
            buffer.TryTakeDisplayText(out _);
            buffer.Append("b");

            buffer.Clear();

            Assert.Equal(string.Empty, buffer.GetFullText());
            Assert.False(buffer.HasContent);
            Assert.False(buffer.TryTakeDisplayText(out string text));
            Assert.Equal(string.Empty, text);
        }

        [Fact]
        public async Task ConcurrentAppend_LosesNoMessages()
        {
            var buffer = new GameLogBuffer(maxDisplayChars: 5_000);
            const int writers = 8;
            const int perWriter = 2_000;

            var flushing = Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                {
                    buffer.TryTakeDisplayText(out _);
                    Task.Yield().GetAwaiter().GetResult();
                }
            });

            await Task.WhenAll(Enumerable.Range(0, writers).Select(w => Task.Run(() =>
            {
                for (int i = 0; i < perWriter; i++) buffer.Append($"w{w}-{i}");
            })));
            await flushing;

            var lines = buffer.GetFullText().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(writers * perWriter, lines.Length);

            for (int w = 0; w < writers; w++)
            {
                var own = lines.Where(l => l.StartsWith($"w{w}-")).Select(l => int.Parse(l.Substring(l.IndexOf('-') + 1))).ToArray();
                Assert.Equal(Enumerable.Range(0, perWriter), own);
            }
        }

        [Fact]
        public void Constructor_RejectsNonPositiveLimit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameLogBuffer(0));
        }
    }
}
