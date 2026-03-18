using System.IO;
using System.Text.Json;

namespace Sunshine;

/// <summary>
///     Smooooll wrapper around a JSON file that persists the installed RBLX state between runs.
///     Basically, holds info about version and mode.
/// </summary>
public static class SunshineState
{
    public static AppState Load()
    {
        try
        {
            if (!File.Exists(Paths.State)) return new AppState();
            var json = File.ReadAllText(Paths.State);
            return JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    public static void Save(AppState state)
    {
        Directory.CreateDirectory(Paths.Base);
        File.WriteAllText(Paths.State,
            JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public class AppState
{
    public string VersionGuid { get; set; } = "";
    public LaunchMode Mode { get; set; } = LaunchMode.Player;
}