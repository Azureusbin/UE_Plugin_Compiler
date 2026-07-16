namespace UEPluginCompiler.Models;

/// <summary>
/// All parameters needed to compile a .uplugin with UAT.
/// </summary>
public record CompileRequest(
    EngineInstall Engine,
    string PluginPath,
    string OutputDir,
    bool Clean = false,
    string TargetPlatforms = "Win64",
    bool NoP4 = true,
    Dictionary<string, string>? EnvVars = null
);
