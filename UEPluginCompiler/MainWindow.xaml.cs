using System.ComponentModel;
using System.Windows;
using UEPluginCompiler.Helpers;
using UEPluginCompiler.Models;
using UEPluginCompiler.ViewModels;
using UEPluginCompiler.Views;

namespace UEPluginCompiler;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) => UpdateMaximizeButton();
        ShowWelcome();
        Logger.Log("MainWindow ready, showing Welcome");
    }

    private void ShowWelcome()
    {
        var welcomeVm = new WelcomeViewModel();
        welcomeVm.FlowOpened += flow =>
        {
            try
            {
                Logger.Log($"Opening flow: {flow.Name} ({flow.Tasks?.Count ?? 0} tasks)");
                Dispatcher.Invoke(() => ShowFlowEditor(flow));
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                DarkDialog.Info("Error", $"Failed to open flow:\n{ex.Message}");
            }
        };
        PageHost.Content = new WelcomePage { DataContext = welcomeVm };
    }

    private void ShowFlowEditor(BuildFlow flow)
    {
        var flowVm = new FlowEditorViewModel();
        flowVm.SetFlow(flow);
        PageHost.Content = new FlowEditorPage(flowVm);
        Logger.Log("FlowEditorPage shown");
    }

    // ─── Title bar ────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) BtnMaximize_Click(sender, e);
        else DragMove();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        // Already on the settings page — don't stack another one
        if (PageHost.Content is SettingsPage) return;

        // Preserve current page before showing settings
        var prevContent = PageHost.Content;
        var settingsVm = new SettingsViewModel();
        settingsVm.NavigateBack += () => PageHost.Content = prevContent;
        PageHost.Content = new SettingsPage { DataContext = settingsVm };
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    private void UpdateMaximizeButton()
    {
        BtnMaximize.Content = WindowState == WindowState.Maximized ? "" : "";
        BtnMaximize.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (PageHost.Content is FlowEditorPage page && page.IsCompiling)
        {
            if (!DarkDialog.Confirm("Compilation In Progress",
                    "A compilation is in progress. Close anyway?", "Close Anyway"))
                e.Cancel = true;
        }
    }
}
