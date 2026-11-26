using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public sealed class FocusScopeTrap
{
    private readonly FocusScopeStack<Control, IInputElement> _stack = new();

    public Control? CurrentScopeRoot => _stack.CurrentScopeRoot;
    public int Depth => _stack.Depth;

    public IDisposable PushScope(Control scopeRoot, IInputElement? returnFocusTo = null, Action? onBack = null)
    {
        if (scopeRoot == null) throw new ArgumentNullException(nameof(scopeRoot));

        returnFocusTo ??= TopLevel.GetTopLevel(scopeRoot)?.FocusManager?.GetFocusedElement();
        _stack.Push(scopeRoot, returnFocusTo, onBack);
        return new ScopeToken(this, scopeRoot);
    }

    public bool PopScope(Control scopeRoot)
    {
        if (!_stack.Pop(scopeRoot, out IInputElement? returnTarget, out bool wasTop))
        {
            return false;
        }

        if (wasTop && CanRestoreFocus(returnTarget))
        {
            var visual = (Visual)returnTarget!;
            TopLevel.GetTopLevel(visual)?.FocusManager?.Focus(returnTarget!, NavigationMethod.Directional, KeyModifiers.None);
        }

        return true;
    }

    public bool TryHandleBack() => _stack.TryHandleBack();

    public void Clear() => _stack.Clear();

    private static bool CanRestoreFocus(IInputElement? element)
    {
        if (element is not Visual visual || !visual.IsAttachedToVisualTree()) return false;
        if (!element.Focusable || !element.IsEffectivelyEnabled) return false;
        if (element is Control control && !control.IsVisible) return false;
        return true;
    }

    private sealed class ScopeToken : IDisposable
    {
        private FocusScopeTrap? _owner;
        private readonly Control _scopeRoot;

        public ScopeToken(FocusScopeTrap owner, Control scopeRoot)
        {
            _owner = owner;
            _scopeRoot = scopeRoot;
        }

        public void Dispose()
        {
            FocusScopeTrap? owner = _owner;
            if (owner == null) return;
            _owner = null;
            owner.PopScope(_scopeRoot);
        }
    }
}
