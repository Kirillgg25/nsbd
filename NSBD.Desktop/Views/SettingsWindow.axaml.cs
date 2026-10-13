using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NSBD.Desktop.Views;

public partial class SettingsWindow : Window
{
    public ObservableCollection<string> RefreshIntervals { get; } = ["1 second", "2 seconds", "5 seconds", "10 seconds"];

    public ObservableCollection<string> CloseBehaviors { get; } = ["Ask every time", "Minimize to tray", "Exit application"];

    public AppSettings? ResultSettings { get; private set; }

    private readonly AppSettings initialSettings;

    public SettingsWindow() : this(new AppSettings())
    {
    }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        DataContext = this;
        initialSettings = settings.Clone();
        ApplySettings(initialSettings);
    }

    private void ApplySettings(AppSettings settings)
    {
        RefreshIntervalBox.SelectedIndex = settings.RefreshIntervalSeconds switch
        {
            2 => 1,
            5 => 2,
            10 => 3,
            _ => 0
        };
        CloseBehaviorBox.SelectedIndex = settings.CloseBehavior switch
        {
            "Minimize" => 1,
            "Exit" => 2,
            _ => 0
        };
        StartMinimizedCheckBox.IsChecked = settings.StartMinimized;
    }

    private void Default_OnClick(object? sender, RoutedEventArgs e) => ApplySettings(SettingsStore.Defaults());

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void Save_OnClick(object? sender, RoutedEventArgs e)
    {
        var interval = RefreshIntervalBox.SelectedIndex switch
        {
            1 => 2,
            2 => 5,
            3 => 10,
            _ => 1
        };
        var behavior = CloseBehaviorBox.SelectedIndex switch
        {
            1 => "Minimize",
            2 => "Exit",
            _ => "Ask"
        };

        ResultSettings = new AppSettings
        {
            RefreshIntervalSeconds = interval,
            CloseBehavior = behavior,
            StartMinimized = StartMinimizedCheckBox.IsChecked == true
        };
        Close();
    }
}
