using System.Text.Json.Serialization;

namespace UEPluginCompiler.Models;

public class BuildFlow
{
    public string Name { get; set; } = "";
    [JsonIgnore] public string? FilePath { get; set; }
    public List<BuildTask> Tasks { get; set; } = [];
    public string OutputDir { get; set; } = "";
}

public class BuildTask
{
    public string Name { get; set; } = "";
    public List<string> EnginePaths { get; set; } = [];
    public string PluginPath { get; set; } = "";
    public string OutputDir { get; set; } = "";
    public Dictionary<string, string> EnvVars { get; set; } = [];
    public bool NoP4 { get; set; } = true;
    public bool CleanBuild { get; set; } = false;

    // Computed display helpers — never serialized
    [JsonIgnore] public string EngineVersionsDisplay =>
        EnginePaths is null || EnginePaths.Count == 0
            ? ""
            : string.Join(", ", EnginePaths.Select(p =>
            {
                var dirName = System.IO.Path.GetFileName(p);
                return dirName.StartsWith("UE_") ? dirName[3..] : dirName;
            }));

    [JsonIgnore] public int EnvVarCount => EnvVars?.Count ?? 0;
    [JsonIgnore] public bool HasEnvVars => EnvVarCount > 0;
    [JsonIgnore] public string EnvVarDisplay =>
        EnvVarCount > 0 ? $"{EnvVarCount} variable(s)" : "";

    [JsonIgnore] public string PluginName =>
        string.IsNullOrWhiteSpace(PluginPath)
            ? ""
            : System.IO.Path.GetFileName(PluginPath);
}
