using System.Windows;
using UEPluginCompiler.Helpers;
using UEPluginCompiler.Models;
using UEPluginCompiler.ViewModels;

namespace UEPluginCompiler.Views;

public partial class TaskEditorDialog : Window
{
    public TaskEditorDialog()
    {
        InitializeComponent();
    }

    private void TitleBar_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    public static async Task<BuildTask?> ShowDialogAsync(Window owner, BuildTask? existingTask = null)
    {
        Logger.Log($"ShowDialogAsync start (edit={existingTask != null})");
        var vm = new TaskEditorViewModel();
        await vm.LoadEnginesAsync();
        Logger.Log($"Engines loaded: {vm.Engines.Count}");

        if (existingTask != null)
            vm.LoadFromTask(existingTask);

        var dialog = new TaskEditorDialog { Owner = owner, DataContext = vm };
        vm.CloseAction = () => dialog.DialogResult = vm.Confirmed;

        var result = dialog.ShowDialog();
        Logger.Log($"Dialog closed: result={result}, confirmed={vm.Confirmed}, task={vm.Task?.Name}");
        return result == true ? vm.Task : null;
    }
}
