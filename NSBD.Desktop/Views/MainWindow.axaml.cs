using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using NSBD.Core;
using NSBD.Desktop;

namespace NSBD.Desktop.Views;

public partial class MainWindow : Window
{
    private AppSettings settings;
    private bool allowClose;
    private bool closeDialogOpen;

    public MainWindow() : this(new AppSettings())
    {
    }

    public MainWindow(AppSettings settings)
    {
        this.settings = settings;
        InitializeComponent();
        Closing += MainWindow_OnClosing;
        Closed += async (_, _) =>
        {
            if (DataContext is ViewModels.MainViewModel viewModel)
            {
                await viewModel.ShutdownAsync();
            }
        };
    }

    public void RequestExit()
    {
        allowClose = true;
        Close();
    }

    public void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private async void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (closeDialogOpen)
        {
            return;
        }

        if (settings.CloseBehavior == "Minimize")
        {
            Hide();
            return;
        }

        if (settings.CloseBehavior == "Exit")
        {
            RequestExit();
            return;
        }

        closeDialogOpen = true;
        var dialog = new CloseChoiceWindow();
        var choice = await dialog.ShowDialog<CloseChoice>(this);
        closeDialogOpen = false;

        if (choice == CloseChoice.Minimize)
        {
            if (dialog.RememberChoice)
            {
                settings.CloseBehavior = "Minimize";
                SettingsStore.Save(settings);
            }

            Hide();
        }
        else if (choice == CloseChoice.Exit)
        {
            if (dialog.RememberChoice)
            {
                settings.CloseBehavior = "Exit";
                SettingsStore.Save(settings);
            }

            RequestExit();
        }
    }

    private void ToggleCapture_OnClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as ViewModels.MainViewModel)?.ToggleCapture();
    }

    private void ClearEvents_OnClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as ViewModels.MainViewModel)?.ClearLiveEvents();
    }

    public async void ShowSettingsFromTray()
    {
        await OpenSettingsAsync();
    }

    private async void Settings_OnClick(object? sender, RoutedEventArgs e)
    {
        await OpenSettingsAsync();
    }

    private async Task OpenSettingsAsync()
    {
        var dialog = new SettingsWindow(settings);
        await dialog.ShowDialog(this);
        if (dialog.ResultSettings is not { } newSettings)
        {
            return;
        }

        settings = newSettings;
        SettingsStore.Save(settings);
        (DataContext as ViewModels.MainViewModel)?.ApplySettings(settings);
    }

    private async void EventsGrid_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || (e.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        if (sender is not DataGrid grid)
        {
            return;
        }

        var selected = grid.SelectedItems.OfType<NetworkEvent>().ToList();
        if (selected.Count == 0 && grid.SelectedItem is NetworkEvent selectedItem)
        {
            selected.Add(selectedItem);
        }

        if (selected.Count == 0 || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        var lines = selected.Select(item => string.Join("\t", new[]
        {
            item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
            item.SourceIp,
            item.DestinationIp,
            item.Port.ToString(),
            item.Protocol,
            item.Service,
            item.DestinationOwner,
            item.Status
        }));

        var header = "Time\tSource\tDestination\tPort\tProtocol\tService\tOwner\tStatus";
        await clipboard.SetTextAsync(string.Join(Environment.NewLine, new[] { header }.Concat(lines)));
        (DataContext as ViewModels.MainViewModel)?.SetFileStatus($"Copied {selected.Count} event(s)");
        e.Handled = true;
    }

    private async void SaveSnapshot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel ||
            TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        try
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save NSBD snapshot",
                SuggestedFileName = $"nsbd-snapshot-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                FileTypeChoices =
                [
                    new FilePickerFileType("NSBD JSON snapshot") { Patterns = ["*.json"] }
                ]
            });

            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await viewModel.SaveSnapshotAsync(stream);
        }
        catch (Exception exception)
        {
            viewModel.SetFileStatus($"Save error: {exception.Message}");
        }
    }

    private async void LoadSnapshot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel ||
            TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load NSBD snapshot",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("NSBD JSON snapshot") { Patterns = ["*.json"] }
                ]
            });

            if (files.Count == 0)
            {
                return;
            }

            await using var stream = await files[0].OpenReadAsync();
            await viewModel.LoadSnapshotAsync(stream);
        }
        catch (Exception exception)
        {
            viewModel.SetFileStatus($"Load error: {exception.Message}");
        }
    }
}
