using System.IO;
using System.Text.Json;

namespace Sunshine;

public class SunshineSettings
{
    // --- activity tracking ---
    public bool EnableActivityTracking { get; set; } = true;

    // --- discord rich presence ---
    public bool DiscordRichPresence { get; set; } = true;
    public bool AllowActivityJoining { get; set; }
    public bool ShowRobloxAccount { get; set; }

    // --- deployment ---
    public bool AutoUpdate { get; set; } = true;
    public bool ForceReinstall { get; set; }
    public bool StaticDirectory { get; set; }
    public string Channel { get; set; } = "production";

    // --- appearance ---
    public bool DarkMode { get; set; } = true;

    // --- global ---
    public bool LaunchOnStartup { get; set; }
    public bool MinimizeToTray { get; set; } = true;

    private static string FilePath => Path.Combine(Paths.Base, "Settings.json");

    public static SunshineSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new SunshineSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<SunshineSettings>(json) ?? new SunshineSettings();
        }
        catch
        {
            return new SunshineSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Paths.Base);
        File.WriteAllText(FilePath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}