using Avalonia.Controls;
using Avalonia.Interactivity;

namespace JTools;

/// <summary>A message, or a question to confirm, shown over a window.</summary>
public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows a message with an OK button.</summary>
    public static Task Show(Window owner, string title, string message)
    {
        var window = Create(title, message);
        window.CancelButton.IsVisible = false;
        window.OkButton.IsCancel = true;
        return window.ShowDialog(owner);
    }

    /// <summary>Asks a question; true if the user chose <paramref name="okText"/>, false if they cancelled.</summary>
    public static Task<bool> Confirm(Window owner, string title, string message, string okText)
    {
        var window = Create(title, message);
        window.OkButton.Content = okText;
        return window.ShowDialog<bool>(owner);
    }

    private static MessageWindow Create(string title, string message)
    {
        var window = new MessageWindow { Title = title };
        window.MessageText.Text = message;
        return window;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
