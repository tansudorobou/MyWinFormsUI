using System.Windows.Input;

namespace Tansu.Forms.Components;

public static partial class Elements
{
    public static Button Button(string text, Action onClick)
    {
        ArgumentNullException.ThrowIfNull(onClick);
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 3, 8, 3) };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>Use native WinForms commanding, including CanExecute and CommandParameter.</summary>
    public static Button Button(string text, ICommand command, object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 3, 8, 3), CommandParameter = parameter, Command = command };
    }

    /// <summary>Awaitable, cancelable action with busy text and automatic duplicate-execution prevention.</summary>
    public static AsyncButton ButtonAsync(string text, Func<CancellationToken, Task> onClick, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(onClick);
        return new AsyncButton(text, onClick, canExecute) { AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 3, 8, 3) };
    }

    public static Label Label(string text) => new() { Text = text, AutoSize = false, UseMnemonic = false, Margin = Padding.Empty };
}
