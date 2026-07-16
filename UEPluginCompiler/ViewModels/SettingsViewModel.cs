using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UEPluginCompiler.Models;
using UEPluginCompiler.Services;

namespace UEPluginCompiler.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly UEDetector _detector = new();
    private readonly SettingsManager _settingsManager = new();

    public ObservableCollection<EngineInstall> Engines { get; } = [];

    public ICommand RefreshEnginesCommand { get; }
    public ICommand AddCustomEngineCommand { get; }
    public ICommand RemoveCustomEngineCommand { get; }
    public ICommand ResetColorsCommand { get; }
    public ICommand BackCommand { get; }

    /// <summary>Raised to navigate back to the previous page.</summary>
    public event Action? NavigateBack;

    private string _statusText = "";
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    // Log colors
    public ObservableCollection<ColorRow> ColorRows { get; } = [];

    private void LoadColors()
    {
        var s = _settingsManager.LoadSettings();
        ColorRows.Clear();
        ColorRows.Add(new ColorRow("Error", s.ErrorColor, _ => SetColor("Error", _)));
        ColorRows.Add(new ColorRow("Warning", s.WarningColor, _ => SetColor("Warning", _)));
        ColorRows.Add(new ColorRow("Success", s.SuccessColor, _ => SetColor("Success", _)));
        ColorRows.Add(new ColorRow("Normal", s.NormalColor, _ => SetColor("Normal", _)));
    }

    public void SetColor(string label, string hex)
    {
        var row = ColorRows.FirstOrDefault(r => r.Label == label);
        if (row == null) return;
        row.UpdateHex(hex);
        SaveColorSettings();
    }

    private void SaveColorSettings()
    {
        var s = _settingsManager.LoadSettings();
        s.ErrorColor = ColorRows[0].Hex;
        s.WarningColor = ColorRows[1].Hex;
        s.SuccessColor = ColorRows[2].Hex;
        s.NormalColor = ColorRows[3].Hex;
        _settingsManager.SaveSettings(s);
    }

    public SettingsViewModel()
    {
        RefreshEnginesCommand = new AsyncRelayCommand(async _ => await LoadEnginesAsync());
        AddCustomEngineCommand = new RelayCommand(_ => AddCustomEngine());
        RemoveCustomEngineCommand = new RelayCommand(p =>
        {
            if (p is EngineInstall e && e.Source == "manual")
            {
                Engines.Remove(e);
                var entries = _settingsManager.LoadCustomEngines();
                entries.RemoveAll(x => string.Equals(x.RootPath, e.RootPath, StringComparison.OrdinalIgnoreCase));
                _settingsManager.SaveCustomEngines(entries);
                StatusText = $"Removed: {e.DisplayName}";
            }
        });
        ResetColorsCommand = new RelayCommand(_ =>
        {
            ColorRows[0].UpdateHex("#E74856");
            ColorRows[1].UpdateHex("#F9A825");
            ColorRows[2].UpdateHex("#16C60C");
            ColorRows[3].UpdateHex("#CCCCCC");
            SaveColorSettings();
            StatusText = "Log colors reset to defaults.";
        });
        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());

        LoadColors();
        _ = LoadEnginesAsync();
    }

    public async Task LoadEnginesAsync()
    {
        StatusText = "Detecting...";
        try
        {
            var detected = await _detector.DetectAllAsync();
            var customEntries = _settingsManager.LoadCustomEngines();
            var customEngines = new List<EngineInstall>();
            foreach (var ce in customEntries)
            {
                var e = _detector.ValidateEngineDirectory(ce.RootPath);
                if (e != null) { e.DisplayName = ce.DisplayName; customEngines.Add(e); }
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Engines.Clear();
                foreach (var e in detected) Engines.Add(e);
                foreach (var e in customEngines)
                {
                    if (!Engines.Any(x => string.Equals(x.RootPath, e.RootPath, StringComparison.OrdinalIgnoreCase)))
                        Engines.Add(e);
                }
            });

            StatusText = $"Found {Engines.Count} engine(s).";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    private void AddCustomEngine()
    {
        var path = Helpers.FolderBrowser.ShowDialog(
            "Select Unreal Engine root folder",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        if (string.IsNullOrWhiteSpace(path)) return;

        var engine = _detector.ValidateEngineDirectory(path);
        if (engine == null)
        {
            MessageBox.Show("Not a valid UE installation.\nExpected: Engine\\Build\\BatchFiles\\RunUAT.bat",
                "Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var displayName = AskForDisplayName(engine.DisplayName);
        if (!string.IsNullOrWhiteSpace(displayName)) engine.DisplayName = displayName;

        Engines.Add(engine);
        var entries = _settingsManager.LoadCustomEngines();
        entries.RemoveAll(e => string.Equals(e.RootPath, engine.RootPath, StringComparison.OrdinalIgnoreCase));
        entries.Add(new CustomEngineEntry { DisplayName = engine.DisplayName, RootPath = engine.RootPath });
        _settingsManager.SaveCustomEngines(entries);
        StatusText = $"Added: {engine.DisplayName}";
    }

    private static string? AskForDisplayName(string defaultName)
    {
        var dialog = new Window
        {
            Title = "Add Custom Engine", Width = 420, Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false, Owner = Application.Current.MainWindow
        };
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock { Text = "Enter a display name:", Margin = new Thickness(0, 0, 0, 6) };
        Grid.SetRow(label, 0); grid.Children.Add(label);

        var tb = new TextBox { Text = defaultName, Margin = new Thickness(0, 0, 0, 12) };
        tb.Loaded += (s, _) => tb.SelectAll();
        Grid.SetRow(tb, 1); grid.Children.Add(tb);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var result = (string?)null;
        var ok = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        ok.Click += (s, _) => { result = tb.Text; dialog.Close(); };
        btnPanel.Children.Add(ok);
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        cancel.Click += (s, _) => dialog.Close();
        btnPanel.Children.Add(cancel);
        Grid.SetRow(btnPanel, 2); grid.Children.Add(btnPanel);

        dialog.Content = grid;
        dialog.ShowDialog();
        return string.IsNullOrWhiteSpace(result) ? defaultName : result;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class ColorRow : INotifyPropertyChanged
{
    public string Label { get; }
    private string _hex;
    public string Hex => _hex;
    public Brush Brush { get; private set; }
    private readonly Action<string> _onChanged;

    public ColorRow(string label, string hex, Action<string> onChanged)
    {
        Label = label; _hex = hex; _onChanged = onChanged;
        Brush = MakeBrush(hex);
    }

    public void UpdateHex(string hex)
    {
        _hex = hex;
        Brush = MakeBrush(hex);
        OnPropertyChanged(nameof(Hex));
        OnPropertyChanged(nameof(Brush));
        _onChanged(hex);
    }

    private static Brush MakeBrush(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return new SolidColorBrush(Colors.Gray); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
