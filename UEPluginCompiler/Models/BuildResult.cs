namespace UEPluginCompiler.Models;

/// <summary>
/// Result returned after a compilation completes, succeeds, or is cancelled.
/// </summary>
public record BuildResult(
    bool Success,
    int ExitCode,
    TimeSpan Duration,
    string? OutputPackage,
    bool WasCancelled
);
