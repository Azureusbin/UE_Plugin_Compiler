using System.Text.Json;
using UEPluginCompiler.Models;

namespace UEPluginCompiler.Services;

public static class FlowSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static BuildFlow Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var flow = JsonSerializer.Deserialize<BuildFlow>(json, Options)
                   ?? throw new InvalidOperationException("Failed to deserialize .uflow file.");
        flow.FilePath = filePath;
        return flow;
    }

    public static void Save(BuildFlow flow, string? filePath = null)
    {
        var path = filePath ?? flow.FilePath
                   ?? throw new InvalidOperationException("No file path specified.");
        var json = JsonSerializer.Serialize(flow, Options);
        File.WriteAllText(path, json);
        flow.FilePath = path;
    }
}
