using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using UEPluginCompiler.Helpers;
using UEPluginCompiler.Models;
using UEPluginCompiler.Services;

namespace UEPluginCompiler.ViewModels;

public class FlowEditorViewModel : INotifyPropertyChanged
{
    private readonly PluginCompiler _compiler = new();
    private CancellationTokenSource? _cts;
    private readonly UEDetector _detector = new();

    public BuildFlow Flow { get; private set; } = new();

    public string Title => string.IsNullOrWhiteSpace(Flow.FilePath)
        ? $"UE Plugin Compiler — {Flow.Name}*"
        : $"UE Plugin Compiler — {System.IO.Path.GetFileName(Flow.FilePath)}";

    public ObservableCollection<BuildTask> Tasks { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];

    private string _globalOutputDir = "";
    public string GlobalOutputDir
    {
        get => _globalOutputDir;
        set { _globalOutputDir = value; OnPropertyChanged(); }
    }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private int _progressPercent;
    public int ProgressPercent
    {
        get => _progressPercent;
        set { _progressPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsProgressIndeterminate)); }
    }
    public bool IsProgressIndeterminate => _progressPercent < 0;

    private bool _isCompiling;
    public bool IsCompiling
    {
        get => _isCompiling;
        set { _isCompiling = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanRun)); OnPropertyChanged(nameof(NotCompiling)); }
    }
    public bool CanRun => !_isCompiling && Tasks.Count > 0;
    public bool NotCompiling => !_isCompiling;

    private bool _isOutputVisible;
    public bool IsOutputVisible
    {
        get => _isOutputVisible;
        set { _isOutputVisible = value; OnPropertyChanged(); }
    }

    private bool _wordWrap = true;
    public bool WordWrap { get => _wordWrap; set { _wordWrap = value; OnPropertyChanged(); } }

    public event Action<string>? OutputLineReceived;

    public ICommand NewFlowCommand { get; }
    public ICommand OpenFlowCommand { get; }
    public ICommand SaveFlowCommand { get; }
    public ICommand SaveAsFlowCommand { get; }
    public ICommand AddTaskCommand { get; }
    public ICommand EditTaskCommand { get; }
    public ICommand CopyTaskCommand { get; }
    public ICommand RemoveTaskCommand { get; }
    public ICommand MoveUpTaskCommand { get; }
    public ICommand MoveDownTaskCommand { get; }
    public ICommand RunAllCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseOutputCommand { get; }

    public FlowEditorViewModel()
    {
        NewFlowCommand = new RelayCommand(_ => NewFlow());
        OpenFlowCommand = new RelayCommand(_ => OpenFlow());
        SaveFlowCommand = new RelayCommand(_ => SaveFlow());
        SaveAsFlowCommand = new RelayCommand(_ => SaveAsFlow());
        AddTaskCommand = new AsyncRelayCommand(async _ => await AddTaskAsync());
        EditTaskCommand = new AsyncRelayCommand(async p => { if (p is BuildTask t) await EditTaskAsync(t); });
        CopyTaskCommand = new RelayCommand(p => { if (p is BuildTask t) CopyTask(t); });
        RemoveTaskCommand = new RelayCommand(p => { if (p is BuildTask t) RemoveTask(t); });
        MoveUpTaskCommand = new RelayCommand(p => { if (p is BuildTask t) MoveTask(t, -1); });
        MoveDownTaskCommand = new RelayCommand(p => { if (p is BuildTask t) MoveTask(t, 1); });
        RunAllCommand = new AsyncRelayCommand(async _ => await RunAllAsync(), _ => CanRun);
        CancelCommand = new RelayCommand(_ => Cancel());
        BrowseOutputCommand = new RelayCommand(_ =>
        {
            var d = Helpers.FolderBrowser.ShowDialog("Select global output directory", GlobalOutputDir);
            if (!string.IsNullOrWhiteSpace(d)) GlobalOutputDir = d;
        });
    }

    // ─── Navigation ───────────────────────────────────────────

    public void SetFlow(BuildFlow flow)
    {
        Flow = flow;
        Tasks.Clear();
        if (flow.Tasks != null)
            foreach (var t in flow.Tasks) Tasks.Add(t);
        GlobalOutputDir = flow.OutputDir ?? "";
        UpdateTitle();
        IsOutputVisible = false;
        LogLines.Clear();
        StatusText = $"Flow: {flow.Name} — {Tasks.Count} task(s)";
    }

    private void UpdateTitle() => OnPropertyChanged(nameof(Title));

    // ─── Flow file ops ────────────────────────────────────────

    private void NewFlow()
    {
        if (Tasks.Count > 0)
        {
            var r = MessageBox.Show("Discard current flow?", "New Flow", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
        }
        SetFlow(new BuildFlow { Name = "Untitled" });
    }

    private void OpenFlow()
    {
        var d = new Microsoft.Win32.OpenFileDialog { Filter = "BuildFlow (*.uflow)|*.uflow", Title = "Open BuildFlow" };
        if (d.ShowDialog() == true)
        {
            try { SetFlow(FlowSerializer.Load(d.FileName)); AddToRecent(d.FileName); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }

    private void SaveFlow()
    {
        if (Flow.FilePath != null) { FlowSerializer.Save(Flow); AddToRecent(Flow.FilePath); UpdateTitle(); StatusText = $"Saved: {Flow.Name}"; }
        else SaveAsFlow();
    }
    private void SaveAsFlow()
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = "BuildFlow (*.uflow)|*.uflow", Title = "Save BuildFlow", DefaultExt = ".uflow" };
        if (d.ShowDialog() == true)
        {
            Flow.OutputDir = GlobalOutputDir;
            Flow.Tasks = Tasks.ToList();
            FlowSerializer.Save(Flow, d.FileName);
            AddToRecent(d.FileName);
            UpdateTitle();
            StatusText = $"Saved: {System.IO.Path.GetFileName(d.FileName)}";
        }
    }

    private static void AddToRecent(string path)
    {
        var sm = new SettingsManager();
        var s = sm.LoadSettings();
        s.RecentFlows.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        s.RecentFlows.Insert(0, path);
        if (s.RecentFlows.Count > 10) s.RecentFlows = s.RecentFlows.Take(10).ToList();
        sm.SaveSettings(s);
    }

    // ─── Task CRUD ────────────────────────────────────────────

    private async Task AddTaskAsync()
    {
        Helpers.Logger.Log("AddTaskAsync: opening dialog");
        var task = await Views.TaskEditorDialog.ShowDialogAsync(Application.Current.MainWindow);
        Helpers.Logger.Log($"AddTaskAsync: dialog returned, task={task?.Name}");
        if (task != null)
        {
            Helpers.Logger.Log($"Before Tasks.Add, count={Tasks.Count}");
            Tasks.Add(task);
            Helpers.Logger.Log($"After Tasks.Add, count={Tasks.Count}");
            try { StatusText = $"Task '{task.Name}' added."; }
            catch (Exception ex) { Helpers.Logger.LogException(ex); throw; }
            Helpers.Logger.Log("After StatusText");
            try { UpdateTitle(); }
            catch (Exception ex) { Helpers.Logger.LogException(ex); throw; }
            Helpers.Logger.Log("After UpdateTitle");
        }
    }

    private async Task EditTaskAsync(BuildTask task)
    {
        Helpers.Logger.Log($"EditTaskAsync: editing '{task.Name}'");
        var idx = Tasks.IndexOf(task);
        var edited = await Views.TaskEditorDialog.ShowDialogAsync(Application.Current.MainWindow, task);
        Helpers.Logger.Log($"EditTaskAsync: dialog returned, edited={edited?.Name}");
        if (edited != null) { Tasks[idx] = edited; StatusText = $"Task '{edited.Name}' updated."; UpdateTitle(); }
    }

    private void CopyTask(BuildTask task)
    {
        var clone = System.Text.Json.JsonSerializer.Deserialize<BuildTask>(
            System.Text.Json.JsonSerializer.Serialize(task))!;
        clone.Name += " (copy)";
        Tasks.Add(clone);
    }

    private void RemoveTask(BuildTask task) { Tasks.Remove(task); UpdateTitle(); }

    private void MoveTask(BuildTask task, int delta)
    {
        var idx = Tasks.IndexOf(task);
        var newIdx = idx + delta;
        if (newIdx < 0 || newIdx >= Tasks.Count) return;
        Tasks.Move(idx, newIdx);
    }

    // ─── Run All ──────────────────────────────────────────────

    private async Task RunAllAsync()
    {
        if (Tasks.Count == 0) return;

        // Ensure Flow.Tasks is synced before running
        Flow.OutputDir = GlobalOutputDir;
        Flow.Tasks = Tasks.ToList();

        // Save before run
        if (Flow.FilePath != null) FlowSerializer.Save(Flow);

        // Detect engines
        var allEngines = new List<EngineInstall>();
        var detected = await _detector.DetectAllAsync();
        allEngines.AddRange(detected);
        var customEntries = new SettingsManager().LoadCustomEngines();
        foreach (var ce in customEntries)
        {
            var e = _detector.ValidateEngineDirectory(ce.RootPath);
            if (e != null) { e.DisplayName = ce.DisplayName; allEngines.Add(e); }
        }

        // Setup logging
        var logsBase = System.IO.Path.Combine(AppContext.BaseDirectory, "Saved", "logs");
        var logDir = System.IO.Path.Combine(logsBase, DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
        try { System.IO.Directory.CreateDirectory(logDir); } catch { }

        IsCompiling = true;
        IsOutputVisible = true;
        LogLines.Clear();
        _cts = new CancellationTokenSource();
        var startTime = DateTime.UtcNow;
        int totalSteps = Tasks.Sum(t => t.EnginePaths.Count);
        int step = 0;
        var allResults = new List<(BuildTask Task, EngineInstall Engine, BuildResult Result)>();

        try
        {
            foreach (var task in Tasks)
            {
                if (_cts.IsCancellationRequested) break;

                foreach (var enginePath in task.EnginePaths)
                {
                    if (_cts.IsCancellationRequested) break;
                    step++;

                    var engine = allEngines.FirstOrDefault(e =>
                        string.Equals(e.RootPath, enginePath, StringComparison.OrdinalIgnoreCase));
                    if (engine == null) { LogLines.Add($"⚠ Engine not found: {enginePath}"); continue; }

                    var outputDir = !string.IsNullOrWhiteSpace(task.OutputDir) ? task.OutputDir : GlobalOutputDir;
                    var finalDir = System.IO.Path.Combine(outputDir, task.Name, engine.Version);
                    var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                        "UEPluginCompiler", Guid.NewGuid().ToString("N"));

                    StatusText = $"Running Task: {task.Name} → UE {engine.Version} ({step}/{totalSteps})";
                    ProgressPercent = (step - 1) * 100 / totalSteps;

                    LogLines.Add("");
                    LogLines.Add($"══════ Task: {task.Name} → UE {engine.Version} ══════");
                    LogLines.Add($"[{step}/{totalSteps}] Output: {finalDir}");

                    var request = new CompileRequest(engine, task.PluginPath, tempDir, task.CleanBuild, "Win64", task.NoP4, task.EnvVars);

                    var outputProgress = new Progress<string>(line =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (LogLines.Count > 5000) LogLines.RemoveAt(0);
                            LogLines.Add(line);
                            OutputLineReceived?.Invoke(line);
                        });
                    });

                    var result = await _compiler.CompileAsync(request, outputProgress, new Progress<int>(), _cts.Token);
                    allResults.Add((task, engine, result));

                    if (result.WasCancelled) break;

                    // Copy from temp to final output dir
                    if (result.Success)
                    {
                        try
                        {
                            CopyDirectory(tempDir, finalDir);
                            LogLines.Add($">>> SUCCESS ({result.Duration.TotalMinutes:F1} min) → {finalDir}");
                        }
                        catch (Exception ex)
                        {
                            LogLines.Add($">>> SUCCESS but copy failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        LogLines.Add($">>> FAILED (exit code {result.ExitCode})");
                    }

                    // Cleanup temp
                    try { System.IO.Directory.Delete(tempDir, true); } catch { }
                }
            }

            IsCompiling = false;
            var elapsed = DateTime.UtcNow - startTime;

            // Summary
            var succeeded = allResults.Where(r => r.Result.Success && !r.Result.WasCancelled).ToList();
            var failed = allResults.Where(r => !r.Result.Success && !r.Result.WasCancelled).ToList();

            LogLines.Add("");
            LogLines.Add("══════ RESULTS SUMMARY ══════");
            foreach (var g in allResults.GroupBy(r => r.Task))
            {
                LogLines.Add($"Task: {g.Key.Name}");
                foreach (var r in g)
                {
                    if (r.Result.WasCancelled) break;
                    LogLines.Add(r.Result.Success
                        ? $"  ✅ UE {r.Engine.Version} — OK ({r.Result.Duration.TotalMinutes:F1} min)"
                        : $"  ❌ UE {r.Engine.Version} — FAILED (exit code {r.Result.ExitCode})");
                }
            }
            LogLines.Add($"Total: {succeeded.Count} OK, {failed.Count} FAIL in {elapsed.TotalMinutes:F1} min");
            LogLines.Add("══════════════════════════════");

            // Log files
            try
            {
                var summaryLines = LogLines.ToList();
                System.IO.File.WriteAllLines(System.IO.Path.Combine(logDir, "_summary.log"), summaryLines);
                var dirs = System.IO.Directory.GetDirectories(logsBase).OrderByDescending(d => d).ToList();
                foreach (var d in dirs.Skip(10)) System.IO.Directory.Delete(d, true);
            }
            catch { }

            if (_cts?.IsCancellationRequested == true) StatusText = "Cancelled.";
            else if (failed.Count == 0) StatusText = $"All {succeeded.Count} OK in {elapsed.TotalMinutes:F1} min.";
            else StatusText = $"{succeeded.Count} OK, {failed.Count} FAIL: {string.Join(", ", failed.Select(r => $"{r.Task.Name}/{r.Engine.Version}"))}";
            ProgressPercent = 100;
        }
        catch (OperationCanceledException) { IsCompiling = false; StatusText = "Cancelled."; }
        catch (Exception ex) { IsCompiling = false; StatusText = $"Error: {ex.Message}"; LogLines.Add($"FATAL: {ex.Message}"); }
        finally { _cts?.Dispose(); _cts = null; }
    }

    private void Cancel() { _cts?.Cancel(); _compiler.Cancel(); StatusText = "Cancelling..."; }

    private static void CopyDirectory(string source, string dest)
    {
        System.IO.Directory.CreateDirectory(dest);
        foreach (var file in System.IO.Directory.GetFiles(source, "*", System.IO.SearchOption.AllDirectories))
        {
            var rel = System.IO.Path.GetRelativePath(source, file);
            var targetFile = System.IO.Path.Combine(dest, rel);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetFile)!);
            System.IO.File.Copy(file, targetFile, overwrite: true);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
