using System.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using UEPluginCompiler.Models;

namespace UEPluginCompiler.Services;

/// <summary>
/// Executes a task's post-build steps against the packaged plugin directory
/// (the temp dir, before it is copied to the final output dir).
/// Delete steps are best-effort (0 matches / per-file IO errors → warning only);
/// copy and run steps are fatal on failure (a missing DLL or a failed script
/// ships a broken plugin), and execution stops at the first fatal error.
/// </summary>
public static class PostBuildRunner
{
    /// <summary>Runs all steps against the package dir. Returns false if a fatal step failed.</summary>
    /// <param name="pluginDir">Directory containing the .uplugin — relative run-script paths resolve against it.</param>
    /// <param name="extraEnv">Extra environment variables injected into run-script processes.</param>
    public static async Task<bool> RunAsync(
        string packageDir,
        IReadOnlyList<PostBuildStep> steps,
        Action<string> log,
        string? pluginDir = null,
        IReadOnlyDictionary<string, string>? extraEnv = null,
        CancellationToken cancellationToken = default)
    {
        var packageRoot = Path.GetFullPath(packageDir);
        bool anyDeletes = false;

        foreach (var step in steps)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            if (step.IsCopy)
            {
                if (!RunCopy(packageRoot, step, log)) return false;
            }
            else if (step.IsRun)
            {
                if (!await RunScriptAsync(packageRoot, step, log, pluginDir, extraEnv, cancellationToken)) return false;
            }
            else if (string.Equals(step.Type, PostBuildStep.TypeDelete, StringComparison.OrdinalIgnoreCase))
            {
                RunDelete(packageRoot, step, log);
                anyDeletes = true;
            }
            else
            {
                log($"  [PostBuild] warning: unknown step type '{step.Type}', skipped");
            }
        }

        // Prune directories emptied by delete steps (mirrors the bat's rd /s /q on Private/Public)
        if (anyDeletes) PruneEmptyDirectories(packageRoot);
        return true;
    }

    private static void RunDelete(string packageRoot, PostBuildStep step, Action<string> log)
    {
        var matches = Match(packageRoot, step.Pattern);
        if (matches.Count == 0)
        {
            log($"  [PostBuild] warning: delete '{step.Pattern}' matched 0 file(s)");
            return;
        }

        int deleted = 0;
        foreach (var rel in matches)
        {
            var fullPath = Path.Combine(packageRoot, rel);
            try
            {
                File.SetAttributes(fullPath, FileAttributes.Normal);
                File.Delete(fullPath);
                deleted++;
            }
            catch (Exception ex)
            {
                log($"  [PostBuild] warning: could not delete '{rel}': {ex.Message}");
            }
        }
        log($"  [PostBuild] delete '{step.Pattern}' — {deleted} file(s) deleted");
    }

    private static bool RunCopy(string packageRoot, PostBuildStep step, Action<string> log)
    {
        try
        {
            var destDir = Path.GetFullPath(Path.Combine(packageRoot, step.Destination ?? ""));
            if (!destDir.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase))
            {
                log($"  [PostBuild] error: copy destination '{step.Destination}' escapes the package dir");
                return false;
            }

            var matches = Match(packageRoot, step.Pattern);
            if (matches.Count == 0)
            {
                log($"  [PostBuild] error: copy '{step.Pattern}' matched 0 file(s)");
                return false;
            }

            Directory.CreateDirectory(destDir);
            foreach (var rel in matches)
            {
                var source = Path.Combine(packageRoot, rel);
                File.Copy(source, Path.Combine(destDir, Path.GetFileName(source)), overwrite: true);
            }
            log($"  [PostBuild] copy '{step.Pattern}' → '{step.Destination}' — {matches.Count} file(s)");
            return true;
        }
        catch (Exception ex)
        {
            log($"  [PostBuild] error: copy '{step.Pattern}' failed: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> RunScriptAsync(
        string packageRoot,
        PostBuildStep step,
        Action<string> log,
        string? pluginDir,
        IReadOnlyDictionary<string, string>? extraEnv,
        CancellationToken cancellationToken)
    {
        // Resolve the script: absolute as-is, relative against the plugin dir first
        // (each plugin ships its own bat next to the .uplugin), then the package dir.
        var script = step.Pattern.Trim();
        if (!Path.IsPathRooted(script))
        {
            var candidates = new[]
            {
                pluginDir != null ? Path.Combine(pluginDir, script) : null,
                Path.Combine(packageRoot, script)
            };
            script = candidates.FirstOrDefault(c => c != null && File.Exists(c)) ?? script;
        }
        if (!File.Exists(script))
        {
            log($"  [PostBuild] error: run script not found: '{step.Pattern}'");
            return false;
        }

        log($"  [PostBuild] run '{script}'");

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{script}\" \"{packageRoot}\"\"",
            WorkingDirectory = packageRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.EnvironmentVariables["PACKAGE_DIR"] = packageRoot;
        if (pluginDir != null) psi.EnvironmentVariables["PLUGIN_DIR"] = pluginDir;
        if (extraEnv != null)
        {
            foreach (var kv in extraEnv)
                psi.EnvironmentVariables[kv.Key] = kv.Value;
        }

        try
        {
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            });

            process.OutputDataReceived += (_, e) => { if (e.Data != null) log($"  | {e.Data}"); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) log($"  | {e.Data}"); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(CancellationToken.None); // cancellation kills the process above

            if (cancellationToken.IsCancellationRequested)
            {
                log($"  [PostBuild] error: run '{step.Pattern}' cancelled");
                return false;
            }
            if (process.ExitCode != 0)
            {
                log($"  [PostBuild] error: run '{step.Pattern}' exited with code {process.ExitCode}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            log($"  [PostBuild] error: run '{step.Pattern}' failed: {ex.Message}");
            return false;
        }
    }

    private static List<string> Match(string packageRoot, string pattern)
    {
        var matcher = new Matcher(); // OrdinalIgnoreCase by default — right for Windows paths
        matcher.AddInclude(pattern.Replace('\\', '/'));
        var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(packageRoot)));
        return result.Files.Select(f => f.Path).ToList();
    }

    private static void PruneEmptyDirectories(string root)
    {
        // Bottom-up so parents that only contained empty dirs get pruned too
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch { /* best-effort, mirrors the bat's silent cleanup */ }
        }
    }
}
