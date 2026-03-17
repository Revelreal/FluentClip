using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace FluentClip.Models;

public class AppSettings
{
    public bool AutoStart { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool RunInBackground { get; set; } = true;
    public bool MonitorClipboard { get; set; } = true;
    public bool DragActionCopy { get; set; } = true;

    public string ShowHotkey { get; set; } = "Ctrl+Shift+V";
    public string SettingsHotkey { get; set; } = "Ctrl+Shift+S";
    public string PinHotkey { get; set; } = "Ctrl+Shift+P";

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FluentClip",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
