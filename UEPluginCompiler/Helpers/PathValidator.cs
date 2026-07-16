namespace UEPluginCompiler.Helpers;

/// <summary>
/// Static helpers for path validation before compilation.
/// </summary>
public static class PathValidator
{
    private const int MaxPathWarningLength = 200;

    /// <summary>
    /// Returns true if the path ends with .uplugin and the file exists.
    /// </summary>
    public static bool IsValidUpluginFile(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && path.EndsWith(".uplugin", StringComparison.OrdinalIgnoreCase)
            && File.Exists(path);
    }

    /// <summary>
    /// Tries to extract the "FriendlyName" from a .uplugin JSON file.
    /// Returns null on failure (doesn't block compilation).
    /// </summary>
    public static string? ExtractFriendlyName(string upluginPath)
    {
        try
        {
            var json = File.ReadAllText(upluginPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("FriendlyName", out var nameEl))
                return nameEl.GetString();
        }
        catch
        {
            // Non-blocking: UAT may still handle malformed JSON
        }

        return null;
    }

    /// <summary>
    /// Tries to extract the module count from a .uplugin JSON file.
    /// Returns -1 on failure.
    /// </summary>
    public static int ExtractModuleCount(string upluginPath)
    {
        try
        {
            var json = File.ReadAllText(upluginPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("Modules", out var modules) && modules.ValueKind == System.Text.Json.JsonValueKind.Array)
                return modules.GetArrayLength();
        }
        catch
        {
            // Non-blocking
        }

        return -1;
    }

    /// <summary>
    /// Returns true if the path may be too long for UAT (common Windows MAX_PATH issue).
    /// </summary>
    public static bool IsLongPath(string path)
    {
        return path.Length > MaxPathWarningLength;
    }

    /// <summary>
    /// Check if the output directory is writable by attempting to create a test file.
    /// </summary>
    public static bool IsDirectoryWritable(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            var testFile = Path.Combine(directoryPath, ".write_test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if the directory looks like a valid UE engine root
    /// (contains Engine/Build/BatchFiles/RunUAT.bat).
    /// </summary>
    public static bool IsValidEngineDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var uatPath = Path.Combine(path, "Engine", "Build", "BatchFiles", "RunUAT.bat");
        return File.Exists(uatPath);
    }
}
