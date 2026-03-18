using System;
using System.IO;

namespace Sunshine;

public static class Paths
{
    // base data directory: %localappdata%\Sunshine
    public static string Base => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sunshine");

    public static string Downloads => Path.Combine(Base, "Downloads");
    public static string Versions => Path.Combine(Base, "Versions");
    public static string Logs => Path.Combine(Base, "Logs");
    public static string State => Path.Combine(Base, "State.json");
}