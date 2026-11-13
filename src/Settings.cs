using System.Text.Json;
using Microsoft.Win32;

namespace Rinse;

class Settings
{
    public bool AutoRinse { get; set; } = true;
    public bool Notify { get; set; } = true;
    public int LinksRinsed { get; set; }
    public int TrackersRemoved { get; set; }
    public bool FirstRunDone { get; set; }

    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rinse");
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* corrupt file: fall back to defaults */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* settings are a convenience; never crash over them */ }
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Opt-in only. Off unless the user ticks it in the tray menu.</summary>
    public static bool StartWithWindows
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                return k?.GetValue("Rinse") != null;
            }
            catch { return false; }
        }
        set
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
                if (value) k.SetValue("Rinse", $"\"{Environment.ProcessPath}\" --tray");
                else k.DeleteValue("Rinse", false);
            }
            catch { }
        }
    }
}
