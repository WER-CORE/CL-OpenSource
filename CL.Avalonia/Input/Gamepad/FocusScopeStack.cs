using System;
using System.Collections.Generic;

namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public sealed class FocusScopeStack<TScope, TFocus>
    where TScope : class
    where TFocus : class
{
    private readonly struct Entry
    {
        public Entry(TScope scopeRoot, TFocus? returnFocusTo, Action? onBack)
        {
            ScopeRoot = scopeRoot;
            ReturnFocusTo = returnFocusTo;
            OnBack = onBack;
        }

        public TScope ScopeRoot { get; }
        public TFocus? ReturnFocusTo { get; }
        public Action? OnBack { get; }
    }

    private readonly List<Entry> _entries = new();

    public int Depth => _entries.Count;

    public TScope? CurrentScopeRoot => _entries.Count > 0 ? _entries[_entries.Count - 1].ScopeRoot : null;

    public Action? CurrentBackHandler => _entries.Count > 0 ? _entries[_entries.Count - 1].OnBack : null;

    public void Push(TScope scopeRoot, TFocus? returnFocusTo, Action? onBack = null)
    {
        if (scopeRoot == null) throw new ArgumentNullException(nameof(scopeRoot));

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_entries[i].ScopeRoot, scopeRoot))
            {
                _entries.RemoveAt(i);
                break;
            }
        }

        _entries.Add(new Entry(scopeRoot, returnFocusTo, onBack));
    }

    public bool Pop(TScope scopeRoot, out TFocus? returnFocusTo, out bool wasTop)
    {
        returnFocusTo = null;
        wasTop = false;
        if (scopeRoot == null || _entries.Count == 0) return false;

        int topIndex = _entries.Count - 1;
        for (int i = topIndex; i >= 0; i--)
        {
            if (ReferenceEquals(_entries[i].ScopeRoot, scopeRoot))
            {
                wasTop = i == topIndex;
                returnFocusTo = wasTop ? _entries[i].ReturnFocusTo : null;
                _entries.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool TryHandleBack()
    {
        Action? handler = CurrentBackHandler;
        if (handler == null) return false;

        handler();
        return true;
    }

    public void Clear()
    {
        _entries.Clear();
    }
}
