using System;
using System.Text;

namespace CL.Core.Services
{
    public sealed class GameLogBuffer
    {
        public const int DefaultMaxDisplayChars = 40_000;

        private readonly object _sync = new object();
        private readonly StringBuilder _full = new StringBuilder();
        private readonly StringBuilder _pending = new StringBuilder();
        private readonly int _maxDisplayChars;
        private string _displayText = string.Empty;
        private bool _flushScheduled;
        private bool _hasContent;

        public GameLogBuffer(int maxDisplayChars = DefaultMaxDisplayChars)
        {
            if (maxDisplayChars < 1) throw new ArgumentOutOfRangeException(nameof(maxDisplayChars));
            _maxDisplayChars = maxDisplayChars;
        }

        public bool HasContent
        {
            get { lock (_sync) return _hasContent; }
        }

        public int FullLength
        {
            get { lock (_sync) return _full.Length; }
        }

        public bool Append(string text)
        {
            lock (_sync)
            {
                if (!_hasContent && !string.IsNullOrWhiteSpace(text)) _hasContent = true;
                _full.AppendLine(text);
                _pending.AppendLine(text);

                if (_flushScheduled) return false;
                _flushScheduled = true;
                return true;
            }
        }

        public bool TryTakeDisplayText(out string text)
        {
            lock (_sync)
            {
                _flushScheduled = false;

                if (_pending.Length == 0)
                {
                    text = _displayText;
                    return false;
                }

                string pending = _pending.ToString();
                _pending.Clear();
                _displayText = Combine(_displayText, pending);
                text = _displayText;
                return true;
            }
        }

        public string GetFullText()
        {
            lock (_sync) return _full.ToString();
        }

        public void Clear()
        {
            lock (_sync)
            {
                _full.Clear();
                _pending.Clear();
                _displayText = string.Empty;
                _hasContent = false;
            }
        }

        private string Combine(string head, string tail)
        {
            int total = head.Length + tail.Length;
            if (total <= _maxDisplayChars) return string.Concat(head, tail);

            int excess = total - _maxDisplayChars;
            int cut = FindLineStart(head, tail, excess);

            if (cut >= total) cut = excess;
            if (cut < total && cut > 0 && char.IsLowSurrogate(CharAt(head, tail, cut)) && char.IsHighSurrogate(CharAt(head, tail, cut - 1)))
            {
                cut++;
            }

            if (cut >= head.Length) return tail.Substring(cut - head.Length);
            return string.Concat(head.AsSpan(cut), tail.AsSpan());
        }

        private static int FindLineStart(string head, string tail, int excess)
        {
            int from = excess - 1;

            if (from < head.Length)
            {
                int idx = head.IndexOf('\n', from);
                if (idx >= 0) return idx + 1;
                from = head.Length;
            }

            int tailIdx = tail.IndexOf('\n', from - head.Length);
            return tailIdx >= 0 ? head.Length + tailIdx + 1 : excess;
        }

        private static char CharAt(string head, string tail, int index)
        {
            return index < head.Length ? head[index] : tail[index - head.Length];
        }
    }
}
