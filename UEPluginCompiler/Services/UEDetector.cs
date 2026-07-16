using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using UEPluginCompiler.Models;

namespace UEPluginCompiler.Services;

/// <summary>
/// Detects Unreal Engine installations via Windows registry,
/// filesystem scanning, and user-added custom paths.
/// </summary>
public class UEDetector
{
    private const string RegistryPath = @"SOFTWARE\EpicGames\Unreal Engine";
    private const string RunUATSubPath = @"Engine\Build\BatchFiles\RunUAT.bat";
    private const string BuildVersionSubPath = @"Engine\Build\Build.version";

    /// <summary>
    /// Run all detection strategies, deduplicate, and return sorted list (newest first).
    /// </summary>
    public async Task<List<EngineInstall>> DetectAllAsync()
    {
        var results = new Dictionary<string, EngineInstall>(StringComparer.OrdinalIgnoreCase);

        // Run registry and filesystem detection in parallel
        var registryTask = Task.Run(DetectFromRegistry);
        var fsTask = Task.Run(DetectFromFileSystem);

        var registryResults = await registryTask;
        var fsResults = await fsTask;

        // Merge: valid engines only, registry wins over filesystem for display name
        foreach (var engine in fsResults.Where(e => e.IsValid))
        {
            var normalized = NormalizePath(engine.RootPath);
            if (!results.ContainsKey(normalized))
                results[normalized] = engine;
        }
        foreach (var engine in registryResults.Where(e => e.IsValid))
        {
            var normalized = NormalizePath(engine.RootPath);
            results[normalized] = engine; // registry entry overwrites filesystem
        }

        var sorted = results.Values
            .OrderByDescending(e => ParseVersion(e.Version))
            .ToList();

        return sorted;
    }

    /// <summary>
    /// Validate a directory as an engine root and create an EngineInstall.
    /// Returns null if the directory is not a valid engine installation.
    /// </summary>
    public EngineInstall? ValidateEngineDirectory(string directoryPath)
    {
        var normalized = NormalizePath(directoryPath);
        var uatPath = Path.Combine(normalized, RunUATSubPath);
        var isValid = File.Exists(uatPath);

        if (!isValid)
            return null;

        var version = ExtractVersionFromDirectory(normalized)
                      ?? ReadBuildVersion(normalized)
                      ?? Path.GetFileName(normalized);

        return new EngineInstall
        {
            Version = version,
            DisplayName = $"UE {version} (Custom)",
            RootPath = normalized,
            IsValid = true,
            Source = "manual"
        };
    }

    // ─── Registry detection ────────────────────────────────────

    private List<EngineInstall> DetectFromRegistry()
    {
        var engines = new List<EngineInstall>();

        // 64-bit view
        try
        {
            using var baseKey64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            engines.AddRange(ReadRegistryEngines(baseKey64, "registry"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registry 64-bit read failed: {ex.Message}");
        }

        // 32-bit view (WOW6432Node for 32-bit launcher installs)
        try
        {
            using var baseKey32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            var engines32 = ReadRegistryEngines(baseKey32, "registry");
            // Merge: skip duplicates already found via 64-bit view
            foreach (var engine in engines32)
            {
                var normalized = NormalizePath(engine.RootPath);
                if (!engines.Any(e => NormalizePath(e.RootPath) == normalized))
                    engines.Add(engine);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registry 32-bit read failed: {ex.Message}");
        }

        return engines;
    }

    private List<EngineInstall> ReadRegistryEngines(RegistryKey baseKey, string source)
    {
        var engines = new List<EngineInstall>();

        try
        {
            using var ueKey = baseKey.OpenSubKey(RegistryPath);
            if (ueKey == null)
                return engines;

            foreach (var subKeyName in ueKey.GetSubKeyNames())
            {
                try
                {
                    using var versionKey = ueKey.OpenSubKey(subKeyName);
                    if (versionKey == null) continue;

                    var installDir = versionKey.GetValue("InstalledDirectory") as string;
                    if (string.IsNullOrWhiteSpace(installDir)) continue;

                    var normalized = NormalizePath(installDir);
                    var uatPath = Path.Combine(normalized, RunUATSubPath);
                    var isValid = Directory.Exists(normalized) && File.Exists(uatPath);

                    engines.Add(new EngineInstall
                    {
                        Version = subKeyName,
                        DisplayName = $"UE {subKeyName} (Epic Launcher)",
                        RootPath = normalized,
                        IsValid = isValid,
                        Source = source
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to read registry key '{subKeyName}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open registry path: {ex.Message}");
        }

        return engines;
    }

    // ─── Filesystem detection ──────────────────────────────────

    private List<EngineInstall> DetectFromFileSystem()
    {
        var engines = new List<EngineInstall>();
        var searchPaths = new[]
        {
            @"C:\Program Files\Epic Games",
            @"C:\Program Files (x86)\Epic Games",
            @"D:\Program Files\Epic Games",
        };

        foreach (var searchPath in searchPaths)
        {
            if (!Directory.Exists(searchPath)) continue;

            try
            {
                foreach (var dir in Directory.GetDirectories(searchPath, "UE_*"))
                {
                    var normalized = NormalizePath(dir);
                    var uatPath = Path.Combine(normalized, RunUATSubPath);
                    if (!File.Exists(uatPath)) continue;

                    var version = ReadBuildVersion(normalized)
                                  ?? ExtractVersionFromDirectory(normalized)
                                  ?? Path.GetFileName(normalized);

                    engines.Add(new EngineInstall
                    {
                        Version = version,
                        DisplayName = $"UE {version}",
                        RootPath = normalized,
                        IsValid = true,
                        Source = "filesystem"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Filesystem scan failed for '{searchPath}': {ex.Message}");
            }
        }

        return engines;
    }

    // ─── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Read Engine/Build/Build.version JSON and return "MajorVersion.MinorVersion".
    /// </summary>
    private static string? ReadBuildVersion(string engineRoot)
    {
        try
        {
            var buildVersionPath = Path.Combine(engineRoot, BuildVersionSubPath);
            if (!File.Exists(buildVersionPath)) return null;

            var json = File.ReadAllText(buildVersionPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            int major = 0, minor = 0;
            if (root.TryGetProperty("MajorVersion", out var majorEl))
                major = majorEl.GetInt32();
            if (root.TryGetProperty("MinorVersion", out var minorEl))
                minor = minorEl.GetInt32();

            if (major > 0)
                return $"{major}.{minor}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read Build.version: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Extract version from directory name like "UE_5.7".
    /// </summary>
    private static string? ExtractVersionFromDirectory(string path)
    {
        var dirName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var match = System.Text.RegularExpressions.Regex.Match(dirName, @"^UE[_]?(\d+\.\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Parse a version string for sorting. Higher = newer.
    /// </summary>
    private static Version ParseVersion(string version)
    {
        if (Version.TryParse(version, out var v))
            return v;
        return new Version(0, 0);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
