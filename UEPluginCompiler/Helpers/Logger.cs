using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace UEPluginCompiler.Helpers;

/// <summary>
/// Debug-only file logger. Writes timestamped messages to debug.log next to the exe.
/// </summary>
public static class Logger
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "Saved", "debug.log");

    private static readonly object _lock = new();

    static Logger()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!); }
        catch { }
        try { File.WriteAllText(LogPath, ""); }
        catch { }
    }

    [Conditional("DEBUG")]
    public static void Log(string message,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var shortFile = file != null ? Path.GetFileName(file) : "?";
        var line2 = $"{DateTime.Now:HH:mm:ss.fff} [{shortFile}:{line} {member}] {message}";
        Debug.WriteLine(line2);
        try
        {
            lock (_lock)
                File.AppendAllText(LogPath, line2 + Environment.NewLine);
        }
        catch { /* best effort */ }
    }

    [Conditional("DEBUG")]
    public static void LogException(Exception ex,
        [CallerMemberName] string? member = null)
    {
        Log($"EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", member);
    }
}
