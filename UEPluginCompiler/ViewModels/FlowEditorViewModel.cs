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

    private bool _isModified;
    public bool IsModified
    {
        get => _isModified;
        set { _isModified = value; OnPropertyChanged(); UpdateTitle(); }
    }

    public string Title
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Flow.FilePath)
                ? Flow.Name
                : System.IO.Path.GetFileName(Flow.FilePath);
            return $"UE Plugin Compiler — {name}{(_isModified ? "*" : "")}";
        }
    }

    public ObservableCollection<BuildTask> Tasks { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];

    private string _globalOutputDir = "";
    public string GlobalOutputDir
    {
        get => _globalOutputDir;
        set { _globalOutputDir = value; OnPropertyChanged(); MarkModified(); }
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
    public event Action? CompilationStarted;
    public event Action<string, int, int>? CompilationCompleted; // outputDir, succeeded, failed
    public event Action? FlowClosed;

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
    public ICommand CloseFlowCommand { get; }
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
        CloseFlowCommand = new RelayCommand(_ => CloseFlow(), _ => NotCompiling);
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
        _globalOutputDir = flow.OutputDir ?? "";
        OnPropertyChanged(nameof(GlobalOutputDir));
        IsOutputVisible = false;
        LogLines.Clear();
        _isModified = false;
        UpdateTitle();
        var displayName = flow.FilePath != null
            ? System.IO.Path.GetFileNameWithoutExtension(flow.FilePath)
            : flow.Name;
        StatusText = $"{displayName} — {Tasks.Count} task(s)";
    }

    private void UpdateTitle() => OnPropertyChanged(nameof(Title));

    private void MarkModified()
    {
        if (!_isModified)
        {
            _isModified = true;
            UpdateTitle();
        }
    }

    // ─── Flow file ops ────────────────────────────────────────

    private void NewFlow()
    {
        if (!TrySaveBeforeDiscard("New Flow", "Discard current flow?")) return;
        SetFlow(new BuildFlow { Name = "Untitled" });
    }

    private void CloseFlow()
    {
        if (!TrySaveBeforeDiscard("Close Flow",
                $"Return to Welcome?\nSave changes before closing.")) return;
        FlowClosed?.Invoke();
    }

    private void OpenFlow()
    {
        if (!TrySaveBeforeDiscard("Open Flow", "Open a different flow?\nSave changes before opening.")) return;
        var d = new Microsoft.Win32.OpenFileDialog { Filter = "BuildFlow (*.uflow)|*.uflow", Title = "Open BuildFlow" };
        if (d.ShowDialog() == true)
        {
            try { SetFlow(FlowSerializer.Load(d.FileName)); AddToRecent(d.FileName); }
            catch (Exception ex) { Views.DarkDialog.Info("Error", $"Failed to open flow:\n{ex.Message}"); }
        }
    }

    /// <summary>
    /// Show save/discard/cancel if the flow is modified. Returns true when safe to proceed
    /// (user chose Save or Discard), false when the action should be cancelled.
    /// Call this before any operation that would discard the current flow.
    /// </summary>
    public bool TrySaveBeforeDiscard(string title, string message)
    {
        if (!_isModified) return true;

        var name = Flow.FilePath != null
            ? System.IO.Path.GetFileNameWithoutExtension(Flow.FilePath)
            : Flow.Name;

        var choice = Views.DarkDialog.SaveDiscardCancel(title,
            $"{message}\n\nSave changes to \"{name}\"?",
            Flow.FilePath != null ? "Save" : "Save As…");

        if (choice == null) return false;        // Cancel
        if (choice == false) return true;         // Discard

        // Save
        if (Flow.FilePath != null)
            DoSave(Flow.FilePath);
        else
            return DoSaveAs();
        return true;
    }

    private void SaveFlow()
    {
        if (Flow.FilePath != null)
            DoSave(Flow.FilePath);
        else
            DoSaveAs();
    }

    public bool DoSaveAs()
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = "BuildFlow (*.uflow)|*.uflow", Title = "Save BuildFlow", DefaultExt = ".uflow" };
        if (d.ShowDialog() != true) return false;
        DoSave(d.FileName);
        return true;
    }

    private void SaveAsFlow() => DoSaveAs();

    public void DoSave(string filePath)
    {
        Flow.OutputDir = GlobalOutputDir;
        Flow.Tasks = Tasks.ToList();
        FlowSerializer.Save(Flow, filePath);
        AddToRecent(filePath);
        _isModified = false;
        UpdateTitle();
        StatusText = $"Saved: {System.IO.Path.GetFileName(filePath)}";
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
            MarkModified();
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
        if (edited != null) { Tasks[idx] = edited; MarkModified(); StatusText = $"Task '{edited.Name}' updated."; UpdateTitle(); }
    }

    private void CopyTask(BuildTask task)
    {
        var clone = System.Text.Json.JsonSerializer.Deserialize<BuildTask>(
            System.Text.Json.JsonSerializer.Serialize(task))!;
        clone.Name += " (copy)";
        Tasks.Add(clone);
        MarkModified();
    }

    private void RemoveTask(BuildTask task) { Tasks.Remove(task); MarkModified(); UpdateTitle(); }

    private void MoveTask(BuildTask task, int delta)
    {
        var idx = Tasks.IndexOf(task);
        var newIdx = idx + delta;
        if (newIdx < 0 || newIdx >= Tasks.Count) return;
        Tasks.Move(idx, newIdx);
        MarkModified();
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
        CompilationStarted?.Invoke();
        _cts = new CancellationTokenSource();
        var startTime = DateTime.UtcNow;
        int totalSteps = Tasks.Sum(t => t.EnginePaths.Count);
        int step = 0;
        var allResults = new List<(BuildTask Task, EngineInstall Engine, BuildResult Result)>();
        var tempRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                    if (engine == null) { Log($"⚠ Engine not found: {enginePath}"); continue; }

                    var outputDir = !string.IsNullOrWhiteSpace(task.OutputDir) ? task.OutputDir : GlobalOutputDir;
                    var pluginName = string.IsNullOrWhiteSpace(task.PluginPath)
                        ? "Plugin" : System.IO.Path.GetFileNameWithoutExtension(task.PluginPath);
                    var finalDir = System.IO.Path.Combine(outputDir, engine.Version, task.Name, pluginName);

                    // Intermediate files live inside the output dir (same drive, easy to find/clean)
                    var tempRoot = System.IO.Path.Combine(outputDir, "_Temp");
                    if (tempRoots.Add(tempRoot))
                        await TryDeleteDirectoryAsync(tempRoot); // purge leftovers from crashed/cancelled runs
                    var tempDir = System.IO.Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));

                    StatusText = $"Running Task: {task.Name} → UE {engine.Version} ({step}/{totalSteps})";
                    ProgressPercent = (step - 1) * 100 / totalSteps;

                    Log("");
                    Log($"══════ Task: {task.Name} → UE {engine.Version} ══════");
                    Log($"[{step}/{totalSteps}] Output: {finalDir}");
                    Log($"[{step}/{totalSteps}] Intermediate: {tempDir}");

                    var request = new CompileRequest(engine, task.PluginPath, tempDir, task.CleanBuild, "Win64", task.NoP4, task.EnvVars);

                    var outputProgress = new Progress<string>(Log);

                    var result = await _compiler.CompileAsync(request, outputProgress, new Progress<int>(), _cts.Token);

                    if (!result.WasCancelled)
                    {
                        if (result.Success)
                        {
                            // Post-build steps run on the temp dir, BEFORE copying to the final output dir,
                            // so the output only ever receives a fully cleaned package.
                            if (task.PostBuildSteps is { Count: > 0 })
                            {
                                Log($"── Post-build: {task.PostBuildSteps.Count} step(s) ──");
                                var stepEnv = new Dictionary<string, string>(task.EnvVars)
                                {
                                    ["ENGINE_VERSION"] = engine.Version,
                                    ["ENGINE_DIR"] = engine.RootPath,
                                    ["TASK_NAME"] = task.Name,
                                    ["OUTPUT_DIR"] = finalDir,
                                };
                                var pbOk = await PostBuildRunner.RunAsync(
                                    tempDir, task.PostBuildSteps, Log,
                                    pluginDir: System.IO.Path.GetDirectoryName(task.PluginPath),
                                    extraEnv: stepEnv,
                                    cancellationToken: _cts.Token);
                                if (!pbOk)
                                {
                                    result = result with { Success = false };
                                    Log(">>> FAILED (post-build step error)");
                                }
                                else
                                {
                                    Log($"── Post-build finished: {task.PostBuildSteps.Count} step(s) OK ──");
                                }
                            }

                            // Copy from temp to final output dir (clean previous products first)
                            if (result.Success)
                            {
                                try
                                {
                                    if (System.IO.Directory.Exists(finalDir))
                                    {
                                        Log($"Cleaning previous output: {finalDir}");
                                        if (!await TryDeleteDirectoryAsync(finalDir))
                                            throw new IOException($"could not delete previous output (files in use?): {finalDir}");
                                    }
                                    CopyDirectory(tempDir, finalDir);
                                    Log($">>> SUCCESS ({result.Duration.TotalMinutes:F1} min) → {finalDir}");
                                }
                                catch (Exception ex)
                                {
                                    result = result with { Success = false };
                                    Log($">>> FAILED (copy to output error: {ex.Message})");
                                }
                            }
                        }
                        else
                        {
                            Log($">>> FAILED (exit code {result.ExitCode})");
                        }
                    }

                    allResults.Add((task, engine, result));

                    // Cleanup temp (retries — a killed UAT may hold file handles briefly)
                    if (!await TryDeleteDirectoryAsync(tempDir))
                        Log($"warning: could not delete intermediate dir: {tempDir}");

                    if (result.WasCancelled) break;
                }
            }

            IsCompiling = false;
            var elapsed = DateTime.UtcNow - startTime;

            var succeeded = allResults.Where(r => r.Result.Success && !r.Result.WasCancelled).ToList();
            var failed = allResults.Where(r => !r.Result.Success && !r.Result.WasCancelled).ToList();

            // Save full log to file before clearing
            try
            {
                var fullLog = LogLines.ToList();
                System.IO.File.WriteAllLines(System.IO.Path.Combine(logDir, "_summary.log"), fullLog);
                var dirs = System.IO.Directory.GetDirectories(logsBase).OrderByDescending(d => d).ToList();
                foreach (var d in dirs.Skip(10)) System.IO.Directory.Delete(d, true);
            }
            catch { }

            // Clear UI log and show summary
            LogLines.Clear();
            OutputLineReceived?.Invoke("");

            if (_cts?.IsCancellationRequested == true)
            {
                LogLines.Add("══════ BUILD FLOW CANCELLED ══════");
                LogLines.Add($"  Cancelled by user after {elapsed.TotalMinutes:F1} min.");
                if (succeeded.Count > 0)
                    LogLines.Add($"  {succeeded.Count} task(s) completed before cancel.");
                LogLines.Add("═════════════════════════════");
                StatusText = $"Cancelled after {succeeded.Count} completed, {elapsed.TotalMinutes:F1} min.";
            }
            else
            {
                LogLines.Add("══════ BUILD FLOW COMPLETE ══════");
                foreach (var g in allResults.GroupBy(r => r.Task))
                {
                    LogLines.Add($"  {g.Key.Name}:");
                    foreach (var r in g)
                    {
                        if (r.Result.WasCancelled) break;
                        LogLines.Add(r.Result.Success
                            ? $"    ✅ UE {r.Engine.Version} — OK ({r.Result.Duration.TotalMinutes:F1} min)"
                            : $"    ❌ UE {r.Engine.Version} — FAILED (exit code {r.Result.ExitCode})");
                    }
                }
                LogLines.Add($"──────────────────────────────");
                LogLines.Add($"  Total: {succeeded.Count} OK, {failed.Count} FAIL in {elapsed.TotalMinutes:F1} min");
                LogLines.Add("══════════════════════════════");

                if (failed.Count == 0) StatusText = $"All {succeeded.Count} OK in {elapsed.TotalMinutes:F1} min.";
                else StatusText = $"{succeeded.Count} OK, {failed.Count} FAIL: {string.Join(", ", failed.Select(r => $"{r.Task.Name}/{r.Engine.Version}"))}";
                CompilationCompleted?.Invoke(GlobalOutputDir, succeeded.Count, failed.Count);
            }
            foreach (var line in LogLines) OutputLineReceived?.Invoke(line);
            ProgressPercent = 100;
        }
        catch (OperationCanceledException) { IsCompiling = false; StatusText = "Cancelled."; }
        catch (Exception ex) { IsCompiling = false; StatusText = $"Error: {ex.Message}"; LogLines.Add($"FATAL: {ex.Message}"); }
        finally
        {
            // Remove intermediate roots entirely (covers cancel/exception paths too)
            foreach (var root in tempRoots)
                await TryDeleteDirectoryAsync(root);
            _cts?.Dispose(); _cts = null;
        }
    }

    /// <summary>Thread-safe log line: appends to LogLines AND streams to the output view.</summary>
    private void Log(string line)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (LogLines.Count > 5000) LogLines.RemoveAt(0);
            LogLines.Add(line);
            OutputLineReceived?.Invoke(line);
        });
    }

    /// <summary>Deletes a directory with retries (a freshly killed UAT can hold handles briefly;
    /// UAT output files are sometimes read-only). Returns true when the directory is gone.</summary>
    private static async Task<bool> TryDeleteDirectoryAsync(string dir, int attempts = 5, int delayMs = 400)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (!System.IO.Directory.Exists(dir)) return true;
                System.IO.Directory.Delete(dir, true);
                return true;
            }
            catch
            {
                try
                {
                    foreach (var f in System.IO.Directory.EnumerateFiles(dir, "*", System.IO.SearchOption.AllDirectories))
                        System.IO.File.SetAttributes(f, System.IO.FileAttributes.Normal);
                }
                catch { }
                await Task.Delay(delayMs);
            }
        }
        return !System.IO.Directory.Exists(dir);
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
