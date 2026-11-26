using System;
using CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;
using Xunit;

namespace CL.Core.Tests;

public class GamepadFocusAndGateTests
{
    [Fact]
    public void FocusScopeStack_NestedScopes_RestoresTargetOnlyWhenTopScopePops()
    {
        var stack = new FocusScopeStack<object, object>();
        var overlayA = new object();
        var overlayB = new object();
        var triggerBtn = new object();
        var innerBtn = new object();

        bool backACalled = false;
        bool backBCalled = false;

        stack.Push(overlayA, triggerBtn, () => backACalled = true);
        Assert.Equal(1, stack.Depth);
        Assert.Same(overlayA, stack.CurrentScopeRoot);

        stack.Push(overlayB, innerBtn, () => backBCalled = true);
        Assert.Equal(2, stack.Depth);
        Assert.Same(overlayB, stack.CurrentScopeRoot);

        Assert.True(stack.TryHandleBack());
        Assert.False(backACalled);
        Assert.True(backBCalled);

        Assert.True(stack.Pop(overlayA, out object? returnFromA, out bool wasTopA));
        Assert.False(wasTopA);
        Assert.Null(returnFromA);
        Assert.Same(overlayB, stack.CurrentScopeRoot);

        Assert.True(stack.Pop(overlayB, out object? returnFromB, out bool wasTopB));
        Assert.True(wasTopB);
        Assert.Same(innerBtn, returnFromB);
        Assert.Equal(0, stack.Depth);
        Assert.Null(stack.CurrentScopeRoot);
    }

    [Fact]
    public void GamepadInputGate_SuppressesWhenUnfocusedOrGameRunning_AndLatchesHeldButtonsOnResume()
    {
        var gate = new GamepadInputGate();
        var repeatState = new GamepadRepeatState();
        Span<UiNavAction> actions = stackalloc UiNavAction[4];

        Assert.True(gate.Evaluate(isWindowActive: true, isWindowMinimized: false, isGameRunning: false, repeatState));

        var southHeld = new GamepadSnapshot(true, 1, GamepadButtonMask.South, 0f, 0f, GamepadGlyphStyle.Xbox);
        Assert.Equal(1, repeatState.Update(southHeld, 16, actions));
        Assert.Equal(UiNavAction.Confirm, actions[0]);

        Assert.False(gate.Evaluate(isWindowActive: true, isWindowMinimized: false, isGameRunning: true, repeatState));
        Assert.True(gate.IsSuppressed);

        Assert.False(gate.Evaluate(isWindowActive: false, isWindowMinimized: false, isGameRunning: false, repeatState));
        Assert.True(gate.IsSuppressed);

        Assert.True(gate.Evaluate(isWindowActive: true, isWindowMinimized: false, isGameRunning: false, repeatState));
        Assert.False(gate.IsSuppressed);

        Assert.Equal(0, repeatState.Update(southHeld, 16, actions));

        Assert.Equal(0, repeatState.Update(new GamepadSnapshot(true, 1, GamepadButtonMask.None, 0f, 0f, GamepadGlyphStyle.Xbox), 16, actions));
        Assert.Equal(1, repeatState.Update(southHeld, 16, actions));
        Assert.Equal(UiNavAction.Confirm, actions[0]);
    }
}
