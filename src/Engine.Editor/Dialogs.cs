using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Engine.Editor;

/// <summary>Lightweight code-only modal dialogs for the editor.</summary>
internal static class Dialogs
{
    /// <summary>
    /// Shows a modal text-input dialog.
    /// Returns the trimmed string the user typed, or null if they cancelled / left it blank.
    /// </summary>
    public static async Task<string?> ShowNameInputAsync(Window owner, string title, string initial = "")
    {
        var nameBox   = new TextBox { Text = initial, Margin = new Thickness(0, 6, 0, 0) };
        var okBtn     = new Button  { Content = "OK",     Width = 72 };
        var cancelBtn = new Button  { Content = "Cancel", Width = 72 };

        var buttons = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(0, 12, 0, 0),
            Spacing             = 8,
        };
        buttons.Children.Add(cancelBtn);
        buttons.Children.Add(okBtn);

        var body = new StackPanel { Margin = new Thickness(16) };
        body.Children.Add(new TextBlock { Text = "Name:" });
        body.Children.Add(nameBox);
        body.Children.Add(buttons);

        var dialog = new Window
        {
            Title                 = title,
            Width                 = 300,
            SizeToContent         = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize             = false,
            ShowInTaskbar         = false,
            Content               = body,
        };

        dialog.Opened   += (_, _) => { nameBox.Focus(); nameBox.SelectAll(); };
        okBtn.Click     += (_, _) => dialog.Close(nameBox.Text?.Trim());
        cancelBtn.Click += (_, _) => dialog.Close((string?)null);
        nameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Return) dialog.Close(nameBox.Text?.Trim());
            if (e.Key == Key.Escape) dialog.Close((string?)null);
        };

        var result = await dialog.ShowDialog<string?>(owner);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    /// <summary>Shows a modal yes/no confirmation dialog. Returns true if the user clicked OK.</summary>
    public static async Task<bool> ShowConfirmAsync(Window owner, string message, string title = "Confirm")
    {
        var okBtn     = new Button { Content = "OK",     Width = 72 };
        var cancelBtn = new Button { Content = "Cancel", Width = 72 };

        var buttons = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(0, 16, 0, 0),
            Spacing             = 8,
        };
        buttons.Children.Add(cancelBtn);
        buttons.Children.Add(okBtn);

        var body = new StackPanel { Margin = new Thickness(16) };
        body.Children.Add(new TextBlock
        {
            Text         = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth     = 260,
        });
        body.Children.Add(buttons);

        var dialog = new Window
        {
            Title                 = title,
            Width                 = 300,
            SizeToContent         = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize             = false,
            ShowInTaskbar         = false,
            Content               = body,
        };

        okBtn.Click     += (_, _) => dialog.Close(true);
        cancelBtn.Click += (_, _) => dialog.Close(false);

        return await dialog.ShowDialog<bool>(owner);
    }
}
