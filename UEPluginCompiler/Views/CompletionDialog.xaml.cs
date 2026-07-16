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
}
