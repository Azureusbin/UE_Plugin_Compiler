using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using UEPluginCompiler.Models;
using UEPluginCompiler.Services;

namespace UEPluginCompiler.ViewModels;

public class WelcomeViewModel : INotifyPropertyChanged
{
    private readonly SettingsManager _settings = new();

    public ObservableCollection<RecentFlowEntry> RecentFlows { get; } = [];

    public ICommand NewFlowCommand { get; }
    public ICommand OpenFlowCommand { get; }
    public ICommand OpenRecentCommand { get; }

    /// <summary>Raised when a flow is opened (new or existing). FlowEditor subscribes.</summary>
    public event Action<BuildFlow>? FlowOpened;

    public WelcomeViewModel()
    {
        NewFlowCommand = new RelayCommand(_ =>
        {
            FlowOpened?.Invoke(new BuildFlow { Name = "Untitled" });
        });

        OpenFlowCommand = new RelayCommand(_ =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "BuildFlow Files (*.uflow)|*.uflow|All Files (*.*)|*.*",
                Title = "Open BuildFlow"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var flow = FlowSerializer.Load(dialog.FileName);
                    AddToRecent(flow.FilePath!);
                    FlowOpened?.Invoke(flow);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open flow:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        });

        OpenRecentCommand = new RelayCommand(param =>
        {
            if (param is RecentFlowEntry entry && File.Exists(entry.FilePath))
            {
                try
                {
                    var flow = FlowSerializer.Load(entry.FilePath);
                    AddToRecent(entry.FilePath);
                    FlowOpened?.Invoke(flow);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open flow:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    RefreshRecentList();
                }
            }
        });

        RefreshRecentList();
    }

    public void RefreshRecentList()
    {
        var settings = _settings.LoadSettings();
        RecentFlows.Clear();
        foreach (var path in settings.RecentFlows)
        {
            if (File.Exists(path))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                var time = File.GetLastWriteTime(path);
                RecentFlows.Add(new RecentFlowEntry
                {
                    FilePath = path,
                    DisplayName = name,
                    LastModified = time
                });
            }
        }
        OnPropertyChanged(nameof(HasRecent));
    }

    public bool HasRecent => RecentFlows.Count > 0;

    private void AddToRecent(string filePath)
    {
        var settings = _settings.LoadSettings();
        settings.RecentFlows.RemoveAll(p => string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase));
        settings.RecentFlows.Insert(0, filePath);
        if (settings.RecentFlows.Count > 10) settings.RecentFlows = settings.RecentFlows.Take(10).ToList();
        _settings.SaveSettings(settings);
        RefreshRecentList();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class RecentFlowEntry
{
    public string FilePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime LastModified { get; set; }
    public string TimeAgo => GetTimeAgo(LastModified);

    private static string GetTimeAgo(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalDays > 30) return $"{dt:yyyy-MM-dd}";
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays} days ago";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h ago";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m ago";
        return "just now";
    }
}
