using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public sealed class GamepadNavigationRouter
{
    private readonly FocusScopeTrap _scopeTrap;
    private readonly ControlActivator _activator;

    public GamepadNavigationRouter(FocusScopeTrap scopeTrap, ControlActivator activator)
    {
        _scopeTrap = scopeTrap;
        _activator = activator;
    }

    public event Action<UiNavAction>? TabNavigationRequested;

    public bool HandleAction(UiNavAction action, TopLevel? activeTopLevel)
    {
        if (activeTopLevel?.FocusManager is not { } focusManager)
        {
            Debug.WriteLine($"[Gamepad][Router] Action={action} dropped: FocusManager is null");
            return false;
        }

        if (XYFocus.GetNavigationModes(activeTopLevel) == XYFocusNavigationModes.Disabled)
        {
            XYFocus.SetNavigationModes(activeTopLevel, XYFocusNavigationModes.Enabled);
        }

        Control? searchRoot = ResolveSearchRoot(activeTopLevel);
        bool result;

        switch (action)
        {
            case UiNavAction.Up:
            case UiNavAction.Down:
            case UiNavAction.Left:
            case UiNavAction.Right:
                result = MoveFocus(focusManager, activeTopLevel, searchRoot, ToNavigationDirection(action));
                break;

            case UiNavAction.Confirm:
                result = _activator.TryActivate(focusManager.GetFocusedElement());
                break;

            case UiNavAction.Back:
                if (focusManager.GetFocusedElement() is ComboBox { IsDropDownOpen: true } openCombo)
                {
                    openCombo.IsDropDownOpen = false;
                    result = true;
                }
                else
                {
                    result = _scopeTrap.TryHandleBack();
                }
                break;

            case UiNavAction.TabPrev:
            case UiNavAction.TabNext:
                if (_scopeTrap.Depth == 0)
                {
                    TabNavigationRequested?.Invoke(action);
                    result = true;
                }
                else
                {
                    result = false;
                }
                break;

            default:
                result = false;
                break;
        }

        IInputElement? focusedAfter = focusManager.GetFocusedElement();
        string focusedLabel = focusedAfter is Control fc
            ? (string.IsNullOrEmpty(fc.Name) ? fc.GetType().Name : $"{fc.GetType().Name}#{fc.Name}")
            : (focusedAfter?.GetType().Name ?? "null");
        Debug.WriteLine($"[Gamepad][Router] Action={action} handled={result} focused={focusedLabel}");

        return result;
    }

    public bool FocusFirstInScope(Control? scopeRoot, TopLevel? activeTopLevel)
    {
        if (activeTopLevel?.FocusManager is not { } focusManager) return false;

        Control? root = scopeRoot ?? (activeTopLevel as Control);
        IInputElement? target = FindFirstFocusableInScope(root, activeTopLevel);
        if (target == null) return false;

        focusManager.Focus(target, NavigationMethod.Directional, KeyModifiers.None);
        (target as Control)?.BringIntoView();
        return true;
    }

    private Control? ResolveSearchRoot(TopLevel activeTopLevel)
    {
        if (_scopeTrap.CurrentScopeRoot is { } scoped &&
            scoped.IsVisible &&
            TopLevel.GetTopLevel(scoped) == activeTopLevel)
        {
            return scoped;
        }

        return activeTopLevel as Control;
    }

    private bool MoveFocus(IFocusManager focusManager, TopLevel activeTopLevel, Control? searchRoot, NavigationDirection direction)
    {
        IInputElement? current = focusManager.GetFocusedElement();
        if (current == null || (searchRoot != null && current is Visual currentVisual && !IsDescendantOrSelf(currentVisual, searchRoot)))
        {
            IInputElement? initial = FindFirstFocusableInScope(searchRoot, activeTopLevel);
            if (initial == null)
            {
                try
                {
                    initial = focusManager.FindFirstFocusableElement();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Gamepad][Router] FindFirstFocusableElement failed: {ex.Message}");
                }
            }

            if (initial == null) return false;

            focusManager.Focus(initial, NavigationMethod.Directional, KeyModifiers.None);
            (initial as Control)?.BringIntoView();
            return true;
        }

        var options = new FindNextElementOptions
        {
            FocusedElement = current,
            SearchRoot = searchRoot
        };

        bool moved = false;
        try
        {
            moved = focusManager.TryMoveFocus(direction, options);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Gamepad][Router] TryMoveFocus({direction}) failed: {ex.Message}");
        }

        if (!moved)
        {
            try
            {
                IInputElement? next = focusManager.FindNextElement(direction, options);
                if (IsValidCandidateInScope(next, current, searchRoot))
                {
                    focusManager.Focus(next!, NavigationMethod.Directional, KeyModifiers.None);
                    moved = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Gamepad][Router] FindNextElement({direction}) failed: {ex.Message}");
            }
        }

        if (!moved)
        {
            NavigationDirection linearDir = direction is NavigationDirection.Left or NavigationDirection.Up
                ? NavigationDirection.Previous
                : NavigationDirection.Next;

            try
            {
                IInputElement? linearNext = focusManager.FindNextElement(linearDir, options);
                if (IsValidCandidateInScope(linearNext, current, searchRoot))
                {
                    focusManager.Focus(linearNext!, NavigationMethod.Directional, KeyModifiers.None);
                    moved = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Gamepad][Router] FindNextElement({linearDir}) failed: {ex.Message}");
            }
        }

        if (moved)
        {
            (focusManager.GetFocusedElement() as Control)?.BringIntoView();
        }

        return moved;
    }

    private static IInputElement? FindFirstFocusableInScope(Control? root, TopLevel activeTopLevel)
    {
        if (root == null) return null;
        return FindFirstFocusableRecursive(root, activeTopLevel);
    }

    private static IInputElement? FindFirstFocusableRecursive(Visual visual, TopLevel activeTopLevel)
    {
        if (!visual.IsVisible) return null;

        if (visual is Control control)
        {
            if (!control.IsEffectivelyEnabled || !control.IsHitTestVisible)
            {
                return null;
            }

            if (!ReferenceEquals(control, activeTopLevel) &&
                control is not LayoutTransformControl &&
                control.Focusable &&
                KeyboardNavigation.GetIsTabStop(control))
            {
                return control;
            }
        }

        foreach (Visual child in visual.GetVisualChildren())
        {
            IInputElement? found = FindFirstFocusableRecursive(child, activeTopLevel);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsValidCandidateInScope(IInputElement? candidate, IInputElement current, Control? searchRoot)
    {
        if (candidate == null || ReferenceEquals(candidate, current)) return false;
        if (searchRoot != null && candidate is Visual v && !IsDescendantOrSelf(v, searchRoot)) return false;
        return true;
    }

    private static bool IsDescendantOrSelf(Visual visual, Control root)
    {
        if (ReferenceEquals(visual, root)) return true;
        return root.IsVisualAncestorOf(visual);
    }

    private static NavigationDirection ToNavigationDirection(UiNavAction action) => action switch
    {
        UiNavAction.Up => NavigationDirection.Up,
        UiNavAction.Down => NavigationDirection.Down,
        UiNavAction.Left => NavigationDirection.Left,
        UiNavAction.Right => NavigationDirection.Right,
        _ => NavigationDirection.Next
    };
}
