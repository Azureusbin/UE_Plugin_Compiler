using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UEPluginCompiler.Models;

/// <summary>
/// Represents a detected or manually added Unreal Engine installation.
/// </summary>
public class EngineInstall : INotifyPropertyChanged
{
    private bool _isValid;
    private bool _isSelected;

    public string Version { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string RootPath { get; set; } = "";
    public string UatPath => System.IO.Path.Combine(RootPath, "Engine", "Build", "BatchFiles", "RunUAT.bat");

    public bool IsValid
    {
        get => _isValid;
        set { _isValid = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    /// <summary>Whether this engine is checked for batch compilation.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string Source { get; set; } = "registry";
    public string StatusText => IsValid ? "Ready" : "Not Found";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
