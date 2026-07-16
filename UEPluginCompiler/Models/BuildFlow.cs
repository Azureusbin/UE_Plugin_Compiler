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
    public List<PostBuildStep> PostBuildSteps { get; set; } = [];

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

    [JsonIgnore] public int PostBuildStepCount => PostBuildSteps?.Count ?? 0;
    [JsonIgnore] public bool HasPostBuildSteps => PostBuildStepCount > 0;
    [JsonIgnore] public string PostBuildStepsDisplay =>
        PostBuildStepCount > 0 ? $"{PostBuildStepCount} step(s)" : "";
}

/// <summary>A cleanup/copy/script action run on the packaged plugin after a successful build,
/// before it is copied to the final output directory.</summary>
public class PostBuildStep
{
    public const string TypeDelete = "delete";
    public const string TypeCopy = "copy";
    public const string TypeRun = "run";

    public string Type { get; set; } = TypeDelete;   // "delete" | "copy" | "run"
    public string Pattern { get; set; } = "";        // delete/copy: glob relative to package dir; run: .bat/.cmd path (absolute or plugin-relative)
    public string? Destination { get; set; }         // copy only: dest dir relative to package dir

    [JsonIgnore] public bool IsCopy => string.Equals(Type, TypeCopy, StringComparison.OrdinalIgnoreCase);
    [JsonIgnore] public bool IsRun => string.Equals(Type, TypeRun, StringComparison.OrdinalIgnoreCase);
}
