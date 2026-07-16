using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UEPluginCompiler.Views;

public partial class CompletionDialog : Window
{
    public CompletionDialog(string message, string path)
    {
        InitializeComponent();
        txtMessage.Text = message;
        txtPath.Text = path;
    }

    private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        if (System.IO.Directory.Exists(txtPath.Text))
            System.Diagnostics.Process.Start("explorer.exe", $"\"{txtPath.Text}\"");
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Show a dark-themed confirmation dialog. Returns true if user confirms.</summary>
    public static bool Confirm(Window owner, string title, string message)
    {
        var dlg = new Window
        {
            Title = title, Width = 400, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xDA, 0xDA, 0xDA)),
            Owner = owner, ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize
        };
        var sp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        sp.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16)
        });
        var btnRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (_, _) => { dlg.DialogResult = false; dlg.Close(); };
        var ok = new System.Windows.Controls.Button { Content = "Close Anyway", Width = 110, Margin = new Thickness(8, 0, 0, 0), IsDefault = true };
        ok.Click += (_, _) => { dlg.DialogResult = true; dlg.Close(); };
        btnRow.Children.Add(cancel);
        btnRow.Children.Add(ok);
        sp.Children.Add(btnRow);
        dlg.Content = sp;
        return dlg.ShowDialog() == true;
    }
}
