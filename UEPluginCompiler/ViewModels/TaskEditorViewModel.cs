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
    public ObservableCollection<PostBuildStepEntry> PostBuildSteps { get; } = [];

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
    public ICommand AddDeleteStepCommand { get; }
    public ICommand AddCopyStepCommand { get; }
    public ICommand AddRunStepCommand { get; }
    public ICommand RemovePostBuildStepCommand { get; }
    public ICommand BrowseStepSourceCommand { get; }
    public ICommand BrowseStepDestCommand { get; }
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
        AddDeleteStepCommand = new RelayCommand(_ => PostBuildSteps.Add(new PostBuildStepEntry { Type = PostBuildStep.TypeDelete }));
        AddCopyStepCommand = new RelayCommand(_ => PostBuildSteps.Add(new PostBuildStepEntry { Type = PostBuildStep.TypeCopy }));
        AddRunStepCommand = new RelayCommand(_ => PostBuildSteps.Add(new PostBuildStepEntry { Type = PostBuildStep.TypeRun }));
        RemovePostBuildStepCommand = new RelayCommand(p =>
        {
            if (p is PostBuildStepEntry e) PostBuildSteps.Remove(e);
        });
        BrowseStepSourceCommand = new RelayCommand(p =>
        {
            if (p is not PostBuildStepEntry entry) return;

            if (entry.IsRun)
            {
                var batDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Batch Scripts (*.bat;*.cmd)|*.bat;*.cmd|All Files (*.*)|*.*",
                    Title = "Select post-build script",
                    InitialDirectory = PluginDir ?? ""
                };
                if (batDialog.ShowDialog() != true) return;
                // Prefer a plugin-relative path (portable), fall back to absolute for external scripts
                entry.Pattern = ToPluginRelativeOrNull(batDialog.FileName) ?? batDialog.FileName;
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Title = "Select source file (inside the plugin directory)",
                InitialDirectory = PluginDir ?? ""
            };
            if (dialog.ShowDialog() != true) return;
            var rel = ToPluginRelative(dialog.FileName);
            if (rel != null) entry.Pattern = rel;
        });
        BrowseStepDestCommand = new RelayCommand(p =>
        {
            if (p is not PostBuildStepEntry entry) return;
            var picked = FolderBrowser.ShowDialog("Select destination folder (inside the plugin directory)", PluginDir);
            if (picked == null) return;
            var rel = ToPluginRelative(picked);
            if (rel != null) entry.Destination = rel == "." ? "" : rel;
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
    }

    /// <summary>Directory containing the selected .uplugin — mirror of the packaged plugin layout.</summary>
    private string? PluginDir =>
        string.IsNullOrWhiteSpace(PluginPath) ? null : Path.GetDirectoryName(PluginPath);

    /// <summary>Converts an absolute path to a plugin-dir-relative, forward-slash path,
    /// or returns null if the path is outside the plugin directory (no warning).</summary>
    private string? ToPluginRelativeOrNull(string picked)
    {
        var pluginDir = PluginDir;
        if (string.IsNullOrEmpty(pluginDir)) return null;
        var rel = Path.GetRelativePath(pluginDir, picked);
        return !rel.StartsWith("..") && !Path.IsPathRooted(rel) ? rel.Replace('\\', '/') : null;
    }

    /// <summary>Converts an absolute path to a plugin-dir-relative, forward-slash path,
    /// or warns and returns null if the path is outside the plugin directory.</summary>
    private string? ToPluginRelative(string picked)
    {
        var rel = ToPluginRelativeOrNull(picked);
        if (rel != null) return rel;
        Views.DarkDialog.Info("Path outside plugin",
            "The selected path is not inside the plugin directory.\n" +
            "Post-build paths are relative to the packaged plugin, so pick a path under the plugin folder or type the relative pattern manually.");
        return null;
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

        PostBuildSteps.Clear();
        foreach (var s in task.PostBuildSteps)
            PostBuildSteps.Add(new PostBuildStepEntry
            {
                Type = s.Type,
                Pattern = s.Pattern,
                Destination = s.Destination ?? ""
            });

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
            if (string.IsNullOrWhiteSpace(TaskName)) { Views.DarkDialog.Info("Edit Task", "Task name is required."); return; }
            if (!Engines.Any(e => e.IsSelected)) { Views.DarkDialog.Info("Edit Task", "Select at least one engine."); return; }
            if (string.IsNullOrWhiteSpace(PluginPath)) { Views.DarkDialog.Info("Edit Task", "Select a .uplugin file."); return; }

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
                    .ToDictionary(e => e.Key, e => e.Value),
                PostBuildSteps = PostBuildSteps
                    .Where(s => !string.IsNullOrWhiteSpace(s.Pattern))
                    .Select(s => new PostBuildStep
                    {
                        Type = s.Type,
                        Pattern = s.Pattern.Trim(),
                        Destination = s.IsCopy ? s.Destination?.Trim() : null
                    })
                    .ToList()
            };
            Helpers.Logger.Log($"Task created: {Task.Name}, paths={Task.EnginePaths.Count}");

            Confirmed = true;
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            Helpers.Logger.LogException(ex);
            Views.DarkDialog.Info("Error", $"Error creating task:\n{ex.Message}");
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

public class PostBuildStepEntry : INotifyPropertyChanged
{
    private string _type = PostBuildStep.TypeDelete;
    private string _pattern = "";
    private string _destination = "";

    public string Type
    {
        get => _type;
        set
        {
            _type = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCopy));
            OnPropertyChanged(nameof(IsRun));
            OnPropertyChanged(nameof(HasSourceBrowse));
            OnPropertyChanged(nameof(TypeLabel));
        }
    }
    public string Pattern { get => _pattern; set { _pattern = value; OnPropertyChanged(); } }
    public string Destination { get => _destination; set { _destination = value; OnPropertyChanged(); } }

    public bool IsCopy => string.Equals(Type, PostBuildStep.TypeCopy, StringComparison.OrdinalIgnoreCase);
    public bool IsRun => string.Equals(Type, PostBuildStep.TypeRun, StringComparison.OrdinalIgnoreCase);
    public bool HasSourceBrowse => IsCopy || IsRun;
    public string TypeLabel => IsCopy ? "COPY" : IsRun ? "RUN" : "DEL";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
