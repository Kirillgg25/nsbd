using System;
using System.IO;
using System.Text.Json;

namespace NSBD.Desktop;

public sealed class AppSettings
{
    public string CloseBehavior { get; set; } = "Ask";
    public int RefreshIntervalSeconds { get; set; } = 1;
    public bool StartMinimized { get; set; }

    public AppSettings Clone() => new()
    {
        CloseBehavior = CloseBehavior,
        RefreshIntervalSeconds = RefreshIntervalSeconds,
        StartMinimized = StartMinimized
    };
}

public static class SettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NSBD",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
            }
        }
        catch
        {
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static AppSettings Defaults() => new();
}
