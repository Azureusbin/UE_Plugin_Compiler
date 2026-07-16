using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UEPluginCompiler.Views;

/// <summary>
/// Dark-themed replacements for MessageBox / simple input dialogs, matching the app's
/// borderless window style (custom title bar, TitleBarBrush strip, WindowBorderBrush edge).
/// </summary>
public static class DarkDialog
{
    /// <summary>Information/error box with a single OK button.</summary>
    public static void Info(string title, string message) =>
        ShowCore(title, message, "OK", showCancel: false);

    /// <summary>Yes/no confirmation. Returns true when the user confirms.</summary>
    public static bool Confirm(string title, string message, string confirmText = "OK") =>
        ShowCore(title, message, confirmText, showCancel: true);

    /// <summary>Single-line text input. Returns null when cancelled.</summary>
    public static string? Prompt(string title, string label, string defaultText = "")
    {
        string? result = null;

        var input = new TextBox
        {
            Text = defaultText,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 16),
            Background = Res<Brush>("InputBgBrush"),
            Foreground = Res<Brush>("TextBrush"),
            BorderBrush = Res<Brush>("BorderBrush"),
            CaretBrush = Res<Brush>("TextBrush"),
        };
        input.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };

        var dlg = BuildWindow(title, out var contentHost, out var buttonRow);

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Res<Brush>("TextBrush"),
            Margin = new Thickness(0, 0, 0, 6)
        });
        stack.Children.Add(input);
        contentHost.Child = stack;

        AddButton(buttonRow, "OK", secondary: false, isDefault: true, isCancel: false,
            onClick: () => { result = input.Text; dlg.Close(); });
        AddButton(buttonRow, "Cancel", secondary: true, isDefault: false, isCancel: true,
            onClick: () => dlg.Close());

        dlg.ShowDialog();
        return result;
    }

    // ─── internals ────────────────────────────────────────────

    private static bool ShowCore(string title, string message, string confirmText, bool showCancel)
    {
        bool confirmed = false;
        var dlg = BuildWindow(title, out var contentHost, out var buttonRow);

        contentHost.Child = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Res<Brush>("TextBrush"),
            Margin = new Thickness(0, 0, 0, 16)
        };

        AddButton(buttonRow, confirmText, secondary: false, isDefault: true, isCancel: !showCancel,
            onClick: () => { confirmed = true; dlg.Close(); });
        if (showCancel)
            AddButton(buttonRow, "Cancel", secondary: true, isDefault: false, isCancel: true,
                onClick: () => dlg.Close());

        dlg.ShowDialog();
        return confirmed;
    }

    /// <summary>Creates the borderless dark window shell: title bar + divider + content + button row.</summary>
    private static Window BuildWindow(string title, out Border contentHost, out StackPanel buttonRow)
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;

        var dlg = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = Res<Brush>("BgBrush"),
            Owner = owner,
            WindowStartupLocation = owner != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen,
        };

        // Title bar (draggable, TitleBarBrush strip with a divider below)
        var titleBar = new Border
        {
            Height = 34,
            Background = Res<Brush>("TitleBarBrush"),
            BorderBrush = Res<Brush>("WindowBorderBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new TextBlock
            {
                Text = "  " + title,
                Foreground = Res<Brush>("TextBrush"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        titleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left) dlg.DragMove();
        };

        contentHost = new Border();
        buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var body = new Grid { Margin = new Thickness(20, 16, 20, 16) };
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(contentHost, 0);
        Grid.SetRow(buttonRow, 1);
        body.Children.Add(contentHost);
        body.Children.Add(buttonRow);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(titleBar, 0);
        Grid.SetRow(body, 1);
        root.Children.Add(titleBar);
        root.Children.Add(body);

        dlg.Content = new Border
        {
            Background = Res<Brush>("BgBrush"),
            BorderBrush = Res<Brush>("WindowBorderBrush"),
            BorderThickness = new Thickness(1),
            Child = root
        };
        return dlg;
    }

    private static void AddButton(StackPanel row, string text, bool secondary, bool isDefault, bool isCancel, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            MinWidth = 88,
            Padding = new Thickness(14, 6, 14, 6),
            Margin = row.Children.Count > 0 ? new Thickness(8, 0, 0, 0) : new Thickness(0),
            IsDefault = isDefault,
            IsCancel = isCancel,
        };
        if (secondary) btn.Style = Res<Style>("SecondaryButtonStyle");
        btn.Click += (_, _) => onClick();
        row.Children.Add(btn);
    }

    private static T Res<T>(string key) => (T)Application.Current.FindResource(key);
}
