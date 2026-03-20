using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sunshine;

/// <summary>
///     The main process, the bootstrapper. Handles basically everything related to downloading, extracting and launching
///     RBLX files.
/// </summary>
public class Bootstrapper
{
    private const string
        DefaultChannel =
            "production"; // TODO: configure this shit with settings (so like if choosing other channel, or RBLX launcher suggests,, you get it)

    private const string CdnBase = "https://setup.rbxcdn.com";
    private const string ClientSettingsCdn = "https://clientsettingscdn.roblox.com";

    private const string AppSettings =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
        "<Settings>\r\n" +
        "  <ContentFolder>content</ContentFolder>\r\n" +
        "  <BaseUrl>http://www.roblox.com</BaseUrl>\r\n" +
        "</Settings>\r\n";

    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(60),
        DefaultRequestHeaders = { { "User-Agent", $"Sunshine/{GetAppVersion()}" } }
    };

    private readonly CancellationTokenSource _cts = new();

    private readonly LaunchMode _mode;
    private PackageManifest? _manifest;
    private string _versionDir = "";

    private string _versionGuid = "";

    public Bootstrapper(LaunchMode mode)
    {
        _mode = mode;
        Logger.WriteLine("Bootstrapper::ctor", $"created for mode={mode}");
    }

    private string ExecutablePath => Path.Combine(
        _versionDir,
        _mode == LaunchMode.Studio ? "RobloxStudioBeta.exe" : "RobloxPlayerBeta.exe");

    // HACK: roblox ships this alongside the player in version folder;
    // if it is not deleted, then when launching the RobloxPlayerBeta.exe, it would launch installer.. stupid???
    private string RobloxInstallerPath => Path.Combine(_versionDir, "RobloxPlayerInstaller.exe");
    
    public bool IsCancelled => _cts.IsCancellationRequested;

    public event Action<string>? StatusChanged;
    public event Action<double>? ProgressChanged; // 0–1

    public void Cancel()
    {
        Logger.WriteLine("Bootstrapper::Cancel", "cancellation requested");
        _cts.Cancel();
    }

    public async Task RunAsync()
    {
        Logger.WriteLine("Bootstrapper::RunAsync", "starting");

        SetStatus("Connecting…");
        await FetchVersionInfoAsync();

        _versionDir = Path.Combine(Paths.Versions, _versionGuid);
        Logger.WriteLine("Bootstrapper::RunAsync", $"version dir: {_versionDir}");

        var previousGuid = SunshineState.Load().VersionGuid;
        var isUpdate = !string.IsNullOrEmpty(previousGuid) && previousGuid != _versionGuid;
        var needsInstall = !File.Exists(ExecutablePath);
        Logger.WriteLine("Bootstrapper::RunAsync", $"needs install: {needsInstall}");
        if (isUpdate)
            Logger.WriteLine("Bootstrapper::RunAsync",
                $"update detected: {previousGuid} → {_versionGuid}");

        if (needsInstall)
        {
            SetStatus(isUpdate ? "Updating Roblox…" : "Downloading Roblox…");
            await DownloadAndInstallAsync();

            if (isUpdate)
                DeleteOldVersions(keepGuid: _versionGuid);
        }
        else
        {
            Logger.WriteLine("Bootstrapper::RunAsync", "roblox already installed, skipping download");
        }
        
        RemoveRobloxInstaller();

        SetStatus("Launching…");
        Launch();
        Logger.WriteLine("Bootstrapper::RunAsync", "done");
    }
    
    // removes RobloxPlayerInstaller.exe
    private void RemoveRobloxInstaller()
    {
        if (!File.Exists(RobloxInstallerPath)) return;

        try
        {
            File.Delete(RobloxInstallerPath);
            Logger.WriteLine("Bootstrapper::RemoveRobloxInstaller", "deleted RobloxPlayerInstaller.exe");
        }
        catch (Exception ex)
        {
            // UHM that would be weird
            Logger.WriteLine("Bootstrapper::RemoveRobloxInstaller",
                $"failed to delete installer: {ex.Message}");
        }
    }
    
    // deletes every version directory that isn't the one we just installed
    private static void DeleteOldVersions(string keepGuid)
    {
        Logger.WriteLine("Bootstrapper::DeleteOldVersions", $"cleaning up old versions (keeping {keepGuid})");

        if (!Directory.Exists(Paths.Versions)) return;

        foreach (var dir in new DirectoryInfo(Paths.Versions).GetDirectories())
        {
            if (dir.Name == keepGuid) continue;

            try
            {
                dir.Delete(recursive: true);
                Logger.WriteLine("Bootstrapper::DeleteOldVersions", $"deleted {dir.Name}");
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Bootstrapper::DeleteOldVersions",
                    $"could not delete {dir.Name}: {ex.Message}");
            }
        }
    }

    // installation here

    private async Task FetchVersionInfoAsync()
    {
        var binaryType = _mode == LaunchMode.Studio ? "WindowsStudio64" : "WindowsPlayer";
        var url = $"{ClientSettingsCdn}/v2/client-version/{binaryType}";
        Logger.WriteLine("Bootstrapper::FetchVersionInfoAsync", $"fetching version info from {url}");

        var json = await GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        _versionGuid = doc.RootElement.GetProperty("clientVersionUpload").GetString()
                       ?? throw new Exception("could not parse version guid from roblox api");

        Logger.WriteLine("Bootstrapper::FetchVersionInfoAsync", $"version guid: {_versionGuid}");

        var manifestUrl = $"{CdnBase}/{_versionGuid}-rbxPkgManifest.txt";
        Logger.WriteLine("Bootstrapper::FetchVersionInfoAsync", $"fetching manifest from {manifestUrl}");

        var manifestText = await GetStringAsync(manifestUrl);
        _manifest = new PackageManifest(manifestText);

        Logger.WriteLine("Bootstrapper::FetchVersionInfoAsync",
            $"manifest parsed: {_manifest.Packages.Count} packages, " +
            $"total packed size: {_manifest.Packages.Sum(p => (long)p.PackedSize) / 1024 / 1024} MB");
    }

    private async Task DownloadAndInstallAsync()
    {
        if (_manifest is null) throw new InvalidOperationException("manifest not loaded");

        Logger.WriteLine("Bootstrapper::DownloadAndInstallAsync", "starting install");

        Directory.CreateDirectory(Paths.Downloads);
        Directory.CreateDirectory(_versionDir);

        var totalBytes = _manifest.Packages.Sum(p => (long)p.PackedSize);
        long downloaded = 0;

        var extractTasks = new List<Task>();

        foreach (var pkg in _manifest.Packages)
        {
            _cts.Token.ThrowIfCancellationRequested();

            SetStatus($"Downloading {pkg.Name}…");
            await DownloadPackageAsync(pkg);

            downloaded += pkg.PackedSize;
            var progress = totalBytes > 0 ? (double)downloaded / totalBytes : 0;
            ProgressChanged?.Invoke(progress);
            Logger.WriteLine("Bootstrapper::DownloadAndInstallAsync",
                $"progress: {progress:P0} ({downloaded / 1024} KB / {totalBytes / 1024} KB)");

            var destDir = Path.Combine(_versionDir, PackageDirectory(pkg.Name));
            var capturedPkg = pkg;
            extractTasks.Add(Task.Run(() => ExtractPackage(capturedPkg, destDir), _cts.Token));
        }

        SetStatus("Extracting…");
        Logger.WriteLine("Bootstrapper::DownloadAndInstallAsync", "waiting for all extractions to finish");
        await Task.WhenAll(extractTasks);

        var appSettingsPath = Path.Combine(_versionDir, "AppSettings.xml");
        await File.WriteAllTextAsync(appSettingsPath, AppSettings, _cts.Token);
        Logger.WriteLine("Bootstrapper::DownloadAndInstallAsync", $"wrote {appSettingsPath}");

        SunshineState.Save(new AppState { VersionGuid = _versionGuid, Mode = _mode });
        Logger.WriteLine("Bootstrapper::DownloadAndInstallAsync", "state saved, install complete");
    }

    private async Task DownloadPackageAsync(Package pkg)
    {
        var dest = pkg.DownloadPath;

        if (File.Exists(dest) && VerifyMd5(dest, pkg.Signature))
        {
            Logger.WriteLine("Bootstrapper::DownloadPackageAsync",
                $"{pkg.Name}: already cached and valid, skipping");
            return;
        }

        var url = $"{CdnBase}/{_versionGuid}-{pkg.Name}";
        Logger.WriteLine("Bootstrapper::DownloadPackageAsync", $"{pkg.Name}: downloading from {url}");

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            _cts.Token.ThrowIfCancellationRequested();
            try
            {
                using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                response.EnsureSuccessStatusCode();

                await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs, _cts.Token);

                Logger.WriteLine("Bootstrapper::DownloadPackageAsync",
                    $"{pkg.Name}: download complete ({new FileInfo(dest).Length / 1024} KB)");
                break;
            }
            catch (OperationCanceledException)
            {
                // clean up the partial file
                if (File.Exists(dest)) File.Delete(dest);
                Logger.WriteLine("Bootstrapper::DownloadPackageAsync", $"{pkg.Name}: cancelled, partial file removed");
                throw;
            }
            catch (Exception ex) when (attempt < 3)
            {
                Logger.WriteLine("Bootstrapper::DownloadPackageAsync",
                    $"{pkg.Name}: attempt {attempt} failed ({ex.Message}), retrying in {attempt * 500} ms");
                if (File.Exists(dest)) File.Delete(dest);
                await Task.Delay(500 * attempt, _cts.Token);
            }
        }

        if (!VerifyMd5(dest, pkg.Signature))
        {
            Logger.WriteLine("Bootstrapper::DownloadPackageAsync",
                $"{pkg.Name}: checksum mismatch after download");
            throw new Exception($"checksum mismatch for {pkg.Name} – download may be corrupt");
        }

        Logger.WriteLine("Bootstrapper::DownloadPackageAsync", $"{pkg.Name}: checksum ok");
    }

    private static void ExtractPackage(Package pkg, string destDir)
    {
        Logger.WriteLine("Bootstrapper::ExtractPackage", $"{pkg.Name} → {destDir}");
        Directory.CreateDirectory(destDir);
        if (!pkg.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var copyDest = Path.Combine(destDir, pkg.Name);
            File.Copy(pkg.DownloadPath, copyDest, true);
            Logger.WriteLine("Bootstrapper::ExtractPackage", $"{pkg.Name}: copied as plain file");
            return;
        }

        using var archive = ZipFile.OpenRead(pkg.DownloadPath);
        var destFull = Path.GetFullPath(destDir) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            // skip pure-directory entries
            if (string.IsNullOrEmpty(entry.Name)) continue;

            var sanitized = entry.FullName
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, '.');

            var destPath = Path.GetFullPath(Path.Combine(destDir, sanitized));

            if (!destPath.StartsWith(destFull, StringComparison.OrdinalIgnoreCase))
            {
                Logger.WriteLine("Bootstrapper::ExtractPackage",
                    $"{pkg.Name}: skipping out-of-bounds entry '{entry.FullName}'");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, true);
        }

        Logger.WriteLine("Bootstrapper::ExtractPackage", $"{pkg.Name}: extracted");
    }

    // so here we launch
    private void Launch()
    {
        Logger.WriteLine("Bootstrapper::Launch", $"starting process: {ExecutablePath}");
        var info = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            WorkingDirectory = _versionDir,
            UseShellExecute = true
        };
        Process.Start(info);
        Logger.WriteLine("Bootstrapper::Launch", "process started");
    }

    private static bool VerifyMd5(string filePath, string expectedHex)
    {
        using var md5 = MD5.Create();
        using var fs = File.OpenRead(filePath);
        var hash = md5.ComputeHash(fs);
        return BitConverter.ToString(hash).Replace("-", "")
            .Equals(expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetStringAsync(string url)
    {
        Logger.WriteLine("Bootstrapper::GetStringAsync", $"GET {url}");
        using var response = await Http.GetAsync(url, _cts.Token);
        response.EnsureSuccessStatusCode();
        Logger.WriteLine("Bootstrapper::GetStringAsync", $"{(int)response.StatusCode} {url}");
        return await response.Content.ReadAsStringAsync(_cts.Token);
    }

    private void SetStatus(string msg)
    {
        Logger.WriteLine("Bootstrapper::SetStatus", msg);
        StatusChanged?.Invoke(msg);
    }

    private static string GetAppVersion()
    {
        try
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0";
        }
        catch
        {
            return "1.0";
        }
    }

    // we map the extract folders...
    private static string PackageDirectory(string packageName)
    {
        return packageName switch
        {
            "RobloxApp.zip" => "",
            "RobloxStudio.zip" => "",
            "Libraries.zip" => "",
            "LibrariesQt5.zip" => "",
            "redist.zip" => "",
            "shaders.zip" => "shaders",
            "ssl.zip" => "ssl",
            "WebView2.zip" => "",
            "WebView2RuntimeInstaller.zip" => "WebView2RuntimeInstaller",
            "content-avatar.zip" => @"content\avatar",
            "content-configs.zip" => @"content\configs",
            "content-fonts.zip" => @"content\fonts",
            "content-sky.zip" => @"content\sky",
            "content-sounds.zip" => @"content\sounds",
            "content-textures2.zip" => @"content\textures",
            "content-models.zip" => @"content\models",
            "content-textures3.zip" => @"PlatformContent\pc\textures",
            "content-terrain.zip" => @"PlatformContent\pc\terrain",
            "content-platform-fonts.zip" => @"PlatformContent\pc\fonts",
            "content-platform-dictionaries.zip" => @"PlatformContent\pc\shared_compression_dictionaries",
            "extracontent-luapackages.zip" => @"ExtraContent\LuaPackages",
            "extracontent-translations.zip" => @"ExtraContent\translations",
            "extracontent-models.zip" => @"ExtraContent\models",
            "extracontent-textures.zip" => @"ExtraContent\textures",
            "extracontent-places.zip" => @"ExtraContent\places",
            "content-studio_svg_textures.zip" => @"content\studio_svg_textures",
            "content-qt_translations.zip" => @"content\qt_translations",
            "content-api-docs.zip" => @"content\api_docs",
            "extracontent-scripts.zip" => @"ExtraContent\scripts",
            "studiocontent-models.zip" => @"StudioContent\models",
            "studiocontent-textures.zip" => @"StudioContent\textures",
            "BuiltInPlugins.zip" => "BuiltInPlugins",
            "BuiltInStandalonePlugins.zip" => "BuiltInStandalonePlugins",
            "ApplicationConfig.zip" => "ApplicationConfig",
            "Plugins.zip" => "Plugins",
            "Qml.zip" => "Qml",
            "StudioFonts.zip" => "StudioFonts",
            "RibbonConfig.zip" => "RibbonConfig",
            _ => ""
        };
    }
}