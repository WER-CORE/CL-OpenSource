using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using CL.Core.Interfaces;

namespace CL_CLegendary_Launcher_.Avalonia.Input.Gamepad;

public interface IGamepadActivatable
{
    bool ActivateFromGamepad();
}

public sealed class ControlActivator
{
    private readonly ISoundService? _soundService;

    public ControlActivator(ISoundService? soundService = null)
    {
        _soundService = soundService;
    }

    public bool TryActivate(IInputElement? focused)
    {
        if (focused == null || !focused.IsEffectivelyEnabled) return false;

        if (focused is IGamepadActivatable custom)
        {
            bool handled = custom.ActivateFromGamepad();
            if (handled) _soundService?.PlayClick();
            return handled;
        }

        if (focused is ToggleSwitch toggleSwitch)
        {
            toggleSwitch.IsChecked = !(toggleSwitch.IsChecked ?? false);
            return true;
        }

        if (focused is CheckBox checkBox)
        {
            checkBox.IsChecked = !(checkBox.IsChecked ?? false);
            return true;
        }

        if (focused is ToggleButton toggleButton)
        {
            toggleButton.IsChecked = !(toggleButton.IsChecked ?? false);
            toggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggleButton));
            if (toggleButton.Command is { } toggleCmd && toggleCmd.CanExecute(toggleButton.CommandParameter))
            {
                toggleCmd.Execute(toggleButton.CommandParameter);
            }
            return true;
        }

        if (focused is Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
            if (button.Command is { } cmd && cmd.CanExecute(button.CommandParameter))
            {
                cmd.Execute(button.CommandParameter);
            }
            return true;
        }

        if (focused is MenuItem menuItem)
        {
            menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, menuItem));
            if (menuItem.Command is { } menuCmd && menuCmd.CanExecute(menuItem.CommandParameter))
            {
                menuCmd.Execute(menuItem.CommandParameter);
            }
            return true;
        }

        if (focused is ComboBox comboBox)
        {
            comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;
            _soundService?.PlayClick();
            return true;
        }

        if (focused is ListBoxItem listBoxItem)
        {
            listBoxItem.IsSelected = true;
            RaiseEnterKeyDown(listBoxItem);
            _soundService?.PlayClick();
            return true;
        }

        if (focused is TreeViewItem treeViewItem)
        {
            treeViewItem.IsSelected = true;
            if (treeViewItem.ItemCount > 0)
            {
                treeViewItem.IsExpanded = !treeViewItem.IsExpanded;
            }
            RaiseEnterKeyDown(treeViewItem);
            _soundService?.PlayClick();
            return true;
        }

        if (focused is InputElement inputElement)
        {
            bool handled = RaiseEnterKeyDown(inputElement);
            if (handled)
            {
                _soundService?.PlayClick();
            }
            return handled;
        }

        return false;
    }

    private static bool RaiseEnterKeyDown(InputElement target)
    {
        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = target,
            Key = Key.Enter
        };
        target.RaiseEvent(args);
        return args.Handled;
    }
}
