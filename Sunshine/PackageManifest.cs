using System;
using System.Collections.Generic;
using System.IO;

namespace Sunshine;

/// <summary>
///     Parses the rbxPkgManifest.txt format used by RBLX's CDN.
///     Format: version line ("v0"), then repeating groups of name / md5 / packed-size / size.
/// </summary>
public class PackageManifest
{
    public PackageManifest(string raw)
    {
        using var reader = new StringReader(raw);

        var version = reader.ReadLine();
        if (version != "v0")
            throw new NotSupportedException($"unknown package manifest version: {version}");

        while (true)
        {
            var name = reader.ReadLine();
            var signature = reader.ReadLine();
            var packedStr = reader.ReadLine();
            var sizeStr = reader.ReadLine();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(signature))
                break;

            // skip the stock launcher
            if (name == "RobloxPlayerLauncher.exe")
                break;

            Packages.Add(new Package
            {
                Name = name,
                Signature = signature,
                PackedSize = int.TryParse(packedStr, out var ps) ? ps : 0,
                Size = int.TryParse(sizeStr, out var s) ? s : 0
            });
        }
    }

    public List<Package> Packages { get; } = new();
}

public class Package
{
    public string Name { get; init; } = "";
    public string Signature { get; init; } = "";
    public int PackedSize { get; init; }
    public int Size { get; init; }

    /// <summary>
    ///     Cached download path inside the sunshine downloads folder.
    /// </summary>
    public string DownloadPath => Path.Combine(Paths.Downloads, Signature);
}