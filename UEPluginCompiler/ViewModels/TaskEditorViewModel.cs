using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using UEPluginCompiler.Helpers;
using UEPluginCompiler.Models;
using UEPluginCompiler.Services;

namespace UEPluginCompiler.ViewModels;

public class TaskEditorViewModel : INotifyPropertyChanged
{
    private readonly UEDetector _detector = new();

    public BuildTask Task { get; set; } = new();
    public bool IsNewTask { get; set; } = true;

    public ObservableCollection<EngineInstall> Engines { get; } = [];
    public ObservableCollection<EnvVarEntry> EnvVars { get; } = [];

    private string _taskName = "";
    public string TaskName
    {
        get => _taskName;
        set { _taskName = value; OnPropertyChanged(); Task.Name = value; }
    }

    private string _pluginPath = "";
    public string PluginPath
    {
        get => _pluginPath;
        set { _pluginPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(PluginInfo)); }
    }

    public string PluginInfo => !string.IsNullOrWhiteSpace(PluginPath) && PluginPath.EndsWith(".uplugin", StringComparison.OrdinalIgnoreCase)
        ? (File.Exists(PluginPath) ? "Selected" : "File not found")
        : "";

    private string _outputDir = "";
    public string OutputDir
    {
        get => _outputDir;
        set { _outputDir = value; OnPropertyChanged(); }
    }

    private bool _noP4 = true;
    public bool NoP4 { get => _noP4; set { _noP4 = value; OnPropertyChanged(); } }

    private bool _cleanBuild;
    public bool CleanBuild { get => _cleanBuild; set { _cleanBuild = value; OnPropertyChanged(); } }

    public ICommand AddEnvVarCommand { get; }
    public ICommand RemoveEnvVarCommand { get; }
    public ICommand BrowsePluginCommand { get; }
    public ICommand OKCommand { get; }
    public ICommand CancelCommand { get; }

    public Action? CloseAction { get; set; }
    public bool Confirmed { get; private set; }

    public TaskEditorViewModel()
    {
        AddEnvVarCommand = new RelayCommand(_ => EnvVars.Add(new EnvVarEntry()));
        RemoveEnvVarCommand = new RelayCommand(p =>
        {
            if (p is EnvVarEntry e) EnvVars.Remove(e);
        });
        BrowsePluginCommand = new RelayCommand(_ =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "UE Plugin Files (*.uplugin)|*.uplugin|All Files (*.*)|*.*",
                Title = "Select .uplugin file"
            };
            if (dialog.ShowDialog() == true)
                PluginPath = dialog.FileName;
        });
        OKCommand = new RelayCommand(_ => Confirm());
        CancelCommand = new RelayCommand(_ => { Confirmed = false; CloseAction?.Invoke(); });

        _ = LoadEnginesAsync();
    }

    /// <summary>Initialize from an existing task for editing.</summary>
    public void LoadFromTask(BuildTask task)
    {
        Task = task;
        TaskName = task.Name;
        PluginPath = task.PluginPath;
        OutputDir = task.OutputDir;
        NoP4 = task.NoP4;
        CleanBuild = task.CleanBuild;

        EnvVars.Clear();
        foreach (var kv in task.EnvVars)
            EnvVars.Add(new EnvVarEntry { Key = kv.Key, Value = kv.Value });

        // Check the engines
        foreach (var engine in Engines)
            engine.IsSelected = task.EnginePaths.Any(p =>
                string.Equals(p, engine.RootPath, StringComparison.OrdinalIgnoreCase));

        IsNewTask = false;
    }

    public async Task LoadEnginesAsync()
    {
        var detected = await _detector.DetectAllAsync();
        var customEntries = new SettingsManager().LoadCustomEngines();
        foreach (var entry in customEntries)
        {
            var e = _detector.ValidateEngineDirectory(entry.RootPath);
            if (e != null) { e.DisplayName = entry.DisplayName; detected.Add(e); }
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Engines.Clear();
            foreach (var engine in detected.OrderByDescending(e => ParseVersion(e.Version)))
            {
                engine.PropertyChanged += (_, _) => { }; // no-op, just to satisfy collection tracking
                Engines.Add(engine);
            }
        });
    }

    private void Confirm()
    {
        try
        {
            Helpers.Logger.Log($"Confirm called: name={TaskName}, engines={Engines.Count(e => e.IsSelected)}, plugin={PluginPath}");
            if (string.IsNullOrWhiteSpace(TaskName)) { MessageBox.Show("Task name is required."); return; }
            if (!Engines.Any(e => e.IsSelected)) { MessageBox.Show("Select at least one engine."); return; }
            if (string.IsNullOrWhiteSpace(PluginPath)) { MessageBox.Show("Select a .uplugin file."); return; }

            Task = new BuildTask
            {
                Name = TaskName,
                PluginPath = PluginPath,
                OutputDir = OutputDir,
                NoP4 = NoP4,
                CleanBuild = CleanBuild,
                EnginePaths = Engines.Where(e => e.IsSelected).Select(e => e.RootPath).ToList(),
                EnvVars = EnvVars
                    .Where(e => !string.IsNullOrWhiteSpace(e.Key))
                    .ToDictionary(e => e.Key, e => e.Value)
            };
            Helpers.Logger.Log($"Task created: {Task.Name}, paths={Task.EnginePaths.Count}");

            Confirmed = true;
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            Helpers.Logger.LogException(ex);
            MessageBox.Show($"Error creating task:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static System.Version ParseVersion(string v)
    {
        if (System.Version.TryParse(v, out var ver)) return ver;
        return new System.Version(0, 0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class EnvVarEntry : INotifyPropertyChanged
{
    private string _key = "";
    private string _value = "";

    public string Key { get => _key; set { _key = value; OnPropertyChanged(); } }
    public string Value { get => _value; set { _value = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
