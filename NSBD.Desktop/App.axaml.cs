using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using NSBD.Desktop.ViewModels;
using NSBD.Desktop.Views;

namespace NSBD.Desktop;

public partial class App : Application
{
    private MainWindow? mainWindow;
    private TrayIcon? trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = SettingsStore.Load();
            mainWindow = new MainWindow(settings)
            {
                DataContext = new MainViewModel(settings),
            };
            desktop.MainWindow = mainWindow;
            SetupTray();

            if (settings.StartMinimized)
            {
                mainWindow.Hide();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTray()
    {
        if (mainWindow is null)
        {
            return;
        }

        using var iconStream = AssetLoader.Open(new Uri("avares://NSBD.Desktop/Assets/nsbd-icon.ico"));
        trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(iconStream),
            ToolTipText = "NSBD Network Monitor",
            IsVisible = true,
            Menu = new NativeMenu()
        };

        var openItem = new NativeMenuItem("Open NSBD");
        openItem.Click += (_, _) => mainWindow.ShowFromTray();
        var settingsItem = new NativeMenuItem("Settings");
        settingsItem.Click += (_, _) => mainWindow.ShowSettingsFromTray();
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => mainWindow.RequestExit();
        trayIcon.Menu.Items.Add(openItem);
        trayIcon.Menu.Items.Add(settingsItem);
        trayIcon.Menu.Items.Add(new NativeMenuItemSeparator());
        trayIcon.Menu.Items.Add(exitItem);
        trayIcon.Clicked += (_, _) => mainWindow.ShowFromTray();

        var icons = new TrayIcons { trayIcon };
        TrayIcon.SetIcons(this, icons);
    }
}
