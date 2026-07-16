using System.Windows;
using System.Windows.Controls;
using UEPluginCompiler.ViewModels;

namespace UEPluginCompiler.Views;

public partial class FlowEditorPage : UserControl
{
    private FlowEditorViewModel? _vm;

    public FlowEditorPage(FlowEditorViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        _vm = vm;
        _vm.OutputLineReceived += OnOutputLine;
    }

    private void OnOutputLine(string line)
    {
        Dispatcher.Invoke(() =>
        {
            txtOutput.AppendText(line + Environment.NewLine);
            txtOutput.ScrollToEnd();
        });
    }

    private void ChkWrap_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && txtOutput != null)
            txtOutput.TextWrapping = cb.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    private void BtnOpenOutput_Click(object sender, RoutedEventArgs e)
    {
        var dir = _vm?.GlobalOutputDir;
        if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
            System.Diagnostics.Process.Start("explorer.exe", $"\"{dir}\"");
    }
}
