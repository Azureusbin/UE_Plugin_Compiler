using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using UEPluginCompiler.Services;
using UEPluginCompiler.ViewModels;

namespace UEPluginCompiler.Views;

public partial class FlowEditorPage : UserControl
{
    private FlowEditorViewModel? _vm;
    public FlowEditorViewModel? ViewModel => _vm;
    public bool IsCompiling => _vm?.IsCompiling ?? false;

    public FlowEditorPage(FlowEditorViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        _vm = vm;
        _vm.OutputLineReceived += AppendColoredLine;
        _vm.CompilationStarted += () => Dispatcher.Invoke(() =>
        {
            txtOutput.Document.Blocks.Clear();
            txtOutput.Document.Blocks.Add(new Paragraph());
        });
        _vm.CompilationCompleted += (dir, succeeded, failed) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (failed > 0)
                {
                    DarkDialog.Info("Build Failed",
                        $"Build completed with failures.\n\n" +
                        $"✅ {succeeded} succeeded\n" +
                        $"❌ {failed} failed\n\n" +
                        $"Check the output log for details.");
                }
                else if (!string.IsNullOrWhiteSpace(dir))
                {
                    var dlg = new CompletionDialog("Compilation finished.", dir);
                    dlg.Owner = Window.GetWindow(this);
                    dlg.ShowDialog();
                }
            });
        };
    }

    private Brush GetBrush(string hex, byte r, byte g, byte b)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return new SolidColorBrush(Color.FromRgb(r, g, b)); }
    }

    private void AppendColoredLine(string line)
    {
        Dispatcher.Invoke(() =>
        {
            var settings = new SettingsManager().LoadSettings();
            var errorBrush = GetBrush(settings.ErrorColor, 0xE7, 0x48, 0x56);
            var warnBrush = GetBrush(settings.WarningColor, 0xF9, 0xA8, 0x25);
            var successBrush = GetBrush(settings.SuccessColor, 0x16, 0xC6, 0x0C);
            var normalBrush = GetBrush(settings.NormalColor, 0xCC, 0xCC, 0xCC);

            var brush = normalBrush;
            var lower = line.ToLowerInvariant();

            if (lower.Contains("error") || lower.Contains("failed") || lower.Contains("❌"))
                brush = errorBrush;
            else if (lower.Contains("warning") || lower.Contains("warn"))
                brush = warnBrush;
            else if (lower.Contains("success") || lower.Contains("✅"))
                brush = successBrush;

            var doc = txtOutput.Document;
            if (doc.Blocks.Count > 1)
            {
                var para = doc.Blocks.FirstBlock as Paragraph;
                if (para != null && para.Inlines.Count > 5000)
                    while (para.Inlines.Count > 4000)
                        para.Inlines.Remove(para.Inlines.FirstInline);
            }

            var run = new Run(line + "\n") { Foreground = brush };
            var lastPara = doc.Blocks.LastBlock as Paragraph;
            lastPara?.Inlines.Add(run);
            txtOutput.ScrollToEnd();
        });
    }

    private void ChkWrap_Changed(object sender, RoutedEventArgs e) { /* RichTextBox always wraps */ }

    private void BtnOpenOutput_Click(object sender, RoutedEventArgs e)
    {
        var dir = _vm?.GlobalOutputDir;
        if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
            System.Diagnostics.Process.Start("explorer.exe", $"\"{dir}\"");
    }
}
