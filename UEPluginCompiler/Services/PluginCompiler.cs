using System.Diagnostics;
using System.Text.RegularExpressions;
using UEPluginCompiler.Models;

namespace UEPluginCompiler.Services;

/// <summary>
/// Executes Unreal Automation Tool (RunUAT.bat) BuildPlugin command
/// with real-time output streaming and cancellation support.
/// </summary>
public class PluginCompiler
{
    private Process? _currentProcess;
    private CancellationTokenRegistration _cancellationRegistration;

    private static readonly Regex ProgressRegex = new(
        @"\[(\d+)/(\d+)\]",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Compile a .uplugin using the specified engine's RunUAT.bat.
    /// </summary>
    /// <param name="request">All compilation parameters.</param>
    /// <param name="outputProgress">Receives one line of stdout/stderr at a time.</param>
    /// <param name="percentProgress">Receives estimated progress [0-100], or -1 when indeterminate.</param>
    /// <param name="cancellationToken">Signals cancellation to terminate the process.</param>
    /// <returns>Result with exit code, duration, and whether it was cancelled.</returns>
    public async Task<BuildResult> CompileAsync(
        CompileRequest request,
        IProgress<string> outputProgress,
        IProgress<int> percentProgress,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        totalSteps = 0;
        currentStep = 0;

        var args = BuildArguments(request);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{request.Engine.UatPath}\" {args}\"",
            WorkingDirectory = request.Engine.RootPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Inject task environment variables
        if (request.EnvVars != null)
        {
            foreach (var kv in request.EnvVars)
                psi.EnvironmentVariables[kv.Key] = kv.Value;
        }

        _currentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var tcs = new TaskCompletionSource<int>();

        // Wire up cancellation
        _cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!_currentProcess.HasExited)
                    _currentProcess.Kill(entireProcessTree: true);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        });

        try
        {
            _currentProcess.OutputDataReceived += (_, e) => ProcessLine(e.Data, outputProgress, percentProgress);
            _currentProcess.ErrorDataReceived += (_, e) => ProcessLine(e.Data, outputProgress, percentProgress);

            _currentProcess.Exited += (_, _) => tcs.TrySetResult(_currentProcess.ExitCode);
            _currentProcess.EnableRaisingEvents = true;

            _currentProcess.Start();
            _currentProcess.BeginOutputReadLine();
            _currentProcess.BeginErrorReadLine();

            var exitCode = await tcs.Task;
            await Task.Run(() => _currentProcess.WaitForExit()); // ensure all output flushed

            var duration = DateTime.UtcNow - startTime;
            var wasCancelled = cancellationToken.IsCancellationRequested;

            return new BuildResult(
                Success: exitCode == 0 && !wasCancelled,
                ExitCode: exitCode,
                Duration: duration,
                OutputPackage: request.OutputDir,
                WasCancelled: wasCancelled
            );
        }
        catch (OperationCanceledException)
        {
            return new BuildResult(false, -1, DateTime.UtcNow - startTime, null, WasCancelled: true);
        }
        catch (Exception ex)
        {
            outputProgress.Report($"Error: {ex.Message}");
            return new BuildResult(false, -1, DateTime.UtcNow - startTime, null, WasCancelled: false);
        }
        finally
        {
            _cancellationRegistration.Dispose();
            _currentProcess?.Dispose();
            _currentProcess = null;
        }
    }

    public void Cancel()
    {
        try
        {
            if (_currentProcess is { HasExited: false })
                _currentProcess.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cancel error: {ex.Message}");
        }
    }

    // ─── Private helpers ───────────────────────────────────────

    private int totalSteps;
    private int currentStep;

    private void ProcessLine(string? line, IProgress<string> output, IProgress<int> percent)
    {
        if (line == null) return;

        output.Report(line);

        // Try to parse progress like "[3/12]" from UAT output
        var match = ProgressRegex.Match(line);
        if (match.Success)
        {
            currentStep = int.Parse(match.Groups[1].Value);
            totalSteps = int.Parse(match.Groups[2].Value);
            percent.Report(totalSteps > 0 ? (currentStep * 100) / totalSteps : -1);
        }
    }

    private static string BuildArguments(CompileRequest request)
    {
        var args = new List<string>
        {
            "BuildPlugin",
            $"-Plugin=\"{request.PluginPath}\"",
            $"-Package=\"{request.OutputDir}\""
        };

        if (!string.IsNullOrWhiteSpace(request.TargetPlatforms))
            args.Add($"-TargetPlatforms={request.TargetPlatforms}");

        if (request.NoP4)
            args.Add("-NoP4");

        if (request.Clean)
            args.Add("-Clean");

        return string.Join(" ", args);
    }
}
