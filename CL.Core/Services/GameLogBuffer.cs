using System;
using System.Collections.Generic;
using System.Text;

namespace CL.Core.Services
{
    public sealed class GameLogBuffer
    {
        public const int DefaultMaxDisplayChars = 40_000;
        public const int DefaultMaxFullChars = 512_000;

        private readonly object _sync = new object();
        private readonly Queue<string> _fullLines = new Queue<string>();
        private readonly StringBuilder _pending = new StringBuilder();
        private readonly int _maxDisplayChars;
        private readonly int _maxFullChars;
        private int _fullLength;
        private bool _isFullHistoryTruncated;
        private string _displayText = string.Empty;
        private bool _flushScheduled;
        private bool _hasContent;
        private bool _isClosed;

        public GameLogBuffer(
            int maxDisplayChars = DefaultMaxDisplayChars,
            int maxFullChars = DefaultMaxFullChars)
        {
            if (maxDisplayChars < 1) throw new ArgumentOutOfRangeException(nameof(maxDisplayChars));
            if (maxFullChars < 1) throw new ArgumentOutOfRangeException(nameof(maxFullChars));
            _maxDisplayChars = maxDisplayChars;
            _maxFullChars = maxFullChars;
        }

        public bool HasContent
        {
            get { lock (_sync) return _hasContent; }
        }

        public int FullLength
        {
            get { lock (_sync) return _fullLength; }
        }

        public bool IsFullHistoryTruncated
        {
            get { lock (_sync) return _isFullHistoryTruncated; }
        }

        public bool Append(string text)
        {
            text ??= string.Empty;

            lock (_sync)
            {
                if (_isClosed) return false;
                if (!_hasContent && !string.IsNullOrWhiteSpace(text)) _hasContent = true;
                AppendToFullLocked(text);
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
            lock (_sync)
            {
                if (_fullLines.Count == 0) return string.Empty;

                var sb = new StringBuilder(_fullLength);
                foreach (string line in _fullLines)
                {
                    sb.Append(line).Append(Environment.NewLine);
                }

                if (sb.Length > _maxFullChars)
                {
                    return sb.ToString(sb.Length - _maxFullChars, _maxFullChars);
                }

                return sb.ToString();
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                ClearLocked();
            }
        }

        public void Close()
        {
            lock (_sync)
            {
                _isClosed = true;
                ClearLocked();
            }
        }

        private void ClearLocked()
        {
            _fullLines.Clear();
            _fullLength = 0;
            _isFullHistoryTruncated = false;
            _pending.Clear();
            _displayText = string.Empty;
            _hasContent = false;
            _flushScheduled = false;
        }

        private void AppendToFullLocked(string text)
        {
            int nl = text.IndexOf('\n');
            if (nl < 0)
            {
                if (text.Length > 0 && text[text.Length - 1] == '\r')
                {
                    text = text.Substring(0, text.Length - 1);
                }

                AppendSingleLineToFullLocked(text);
                return;
            }

            int pos = 0;
            while (true)
            {
                int lineEnd = nl >= 0 ? nl : text.Length;
                if (lineEnd > pos && text[lineEnd - 1] == '\r')
                {
                    lineEnd--;
                }

                string line = lineEnd == pos ? string.Empty : text.Substring(pos, lineEnd - pos);
                AppendSingleLineToFullLocked(line);

                if (nl < 0)
                {
                    break;
                }

                pos = nl + 1;
                nl = pos < text.Length ? text.IndexOf('\n', pos) : -1;
            }
        }

        private void AppendSingleLineToFullLocked(string line)
        {
            int newLineLen = Environment.NewLine.Length;
            int entryLen = line.Length + newLineLen;

            if (entryLen > _maxFullChars)
            {
                if (_fullLines.Count > 0)
                {
                    _fullLines.Clear();
                    _fullLength = 0;
                }

                _isFullHistoryTruncated = true;

                int maxBody = Math.Max(0, _maxFullChars - newLineLen);
                if (maxBody == 0)
                {
                    _fullLines.Enqueue(string.Empty);
                    _fullLength = _maxFullChars;
                    return;
                }

                int start = line.Length - maxBody;
                if (start > 0 && start < line.Length &&
                    char.IsLowSurrogate(line[start]) && char.IsHighSurrogate(line[start - 1]))
                {
                    start++;
                }

                string trimmed = line.Substring(start);
                _fullLines.Enqueue(trimmed);
                _fullLength = trimmed.Length + newLineLen;
                return;
            }

            _fullLines.Enqueue(line);
            _fullLength += entryLen;

            while (_fullLines.Count > 1 && _fullLength > _maxFullChars)
            {
                string removed = _fullLines.Dequeue();
                _fullLength -= (removed.Length + newLineLen);
                _isFullHistoryTruncated = true;
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
