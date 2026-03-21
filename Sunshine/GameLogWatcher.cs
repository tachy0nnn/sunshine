using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sunshine;

/// <summary>
///     Parsews Roblox's logs for detecting if player joined, disconnected, teleported or etc-era.
/// </summary>
public class GameLogWatcher : IDisposable
{
    // log entry markers we care about
    private const string EntryGameJoining = "[FLog::Output] ! Joining game";
    private const string EntryGameJoined = "[FLog::Network] serverId:";
    private const string EntryGameDisconnected = "[FLog::Network] Time to disconnect replication data:";
    private const string EntryGameLeaving = "[FLog::SingleSurfaceApp] leaveUGCGameInternal";
    private const string EntryGameTeleporting = "[FLog::GameJoinUtil] GameJoinUtil::initiateTeleportToPlace";
    private const string EntryUdmux = "[FLog::Network] UDMUX Address = ";

    private const string PatternGameJoining = @"! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)";
    private const string PatternGameJoined = @"serverId: ([0-9\.]+)\|[0-9]+";

    private const string PatternUdmux =
        @"UDMUX Address = ([0-9\.]+), Port = [0-9]+ \| RCC Server Address = ([0-9\.]+), Port = [0-9]+";

    private int _entriesRead;

    private bool _teleportPending;

    public bool InGame { get; private set; }
    private bool IsDisposed { get; set; }

    public GameActivityData Current { get; private set; } = new();

    /// <summary>ordered newest→oldest</summary>
    private List<GameActivityData> History { get; } = [];

    public void Dispose()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    public event EventHandler? OnGameJoin;
    public event EventHandler? OnGameLeave;
    public event EventHandler? OnLogOpen;

    // TODO: avoid using 'async' for method with the 'void' return type or catch all exceptions in it: any exceptions unhandled by the method might lead to the process crash
    // got it out of rider
    public async void Start()
    {
        const string id = "GameLogWatcher::Start";

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox", "logs");

        if (!Directory.Exists(logDir))
        {
            Logger.WriteLine(id, "roblox log directory not found, aborting");
            return;
        }

        // wait for a log file that was created in the last ~15 seconds
        // (roblox creates it pretty quickly after launch but not instantly)
        FileInfo logFile;
        while (true)
        {
            logFile = new DirectoryInfo(logDir)
                .GetFiles()
                .Where(f => f.Name.Contains("Player", StringComparison.OrdinalIgnoreCase)
                            && f.CreationTime <= DateTime.Now)
                .OrderByDescending(f => f.CreationTime)
                .FirstOrDefault()!;

            if (logFile.CreationTime.AddSeconds(15) > DateTime.Now)
                break;

            Logger.WriteLine(id, $"waiting for a fresh log file (newest: {logFile?.Name ?? "none"})");
            await Task.Delay(1000);
        }

        OnLogOpen?.Invoke(this, EventArgs.Empty);
        Logger.WriteLine(id, $"opened {logFile.FullName}");

        await using var stream = logFile.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        while (!IsDisposed)
        {
            var line = await reader.ReadLineAsync();

            if (line is null)
                await Task.Delay(500);
            else
                ProcessLine(line);
        }
    }

    private void ProcessLine(string line)
    {
        const string id = "GameLogWatcher::ProcessLine";

        _entriesRead++;

        // lightweight progress logging without spamming
        if (_entriesRead <= 1000 && _entriesRead % 50 == 0)
            Logger.WriteLine(id, $"read {_entriesRead} log entries");
        else if (_entriesRead % 100 == 0)
            Logger.WriteLine(id, $"read {_entriesRead} log entries");

        // strip the timestamp prefix so comparisons are cheaper
        var msgStart = line.IndexOf(' ');
        if (msgStart == -1) return;
        var msg = line[(msgStart + 1)..];

        // user returned to the desktop app shell
        if (msg.StartsWith(EntryGameLeaving))
        {
            Logger.WriteLine(id, "user returned to desktop app");
            if (Current.PlaceId != 0 && !InGame)
            {
                Logger.WriteLine(id, "stale join data, resetting");
                Current = new GameActivityData();
            }

            return;
        }

        if (!InGame && Current.PlaceId == 0)
        {
            // watch for a join attempt
            if (msg.StartsWith(EntryGameJoining))
            {
                var match = Regex.Match(msg, PatternGameJoining);
                if (match.Groups.Count != 4)
                {
                    Logger.WriteLine(id, $"unexpected game join format: {msg}");
                    return;
                }

                Current = new GameActivityData
                {
                    PlaceId = long.Parse(match.Groups[2].Value),
                    JobId = match.Groups[1].Value,
                    MachineAddress = match.Groups[3].Value,
                    IsTeleport = _teleportPending
                };

                _teleportPending = false;
                Logger.WriteLine(id, $"joining game ({Current})");
            }
        }
        else if (!InGame && Current.PlaceId != 0)
        {
            // wait for server confirmation
            if (msg.StartsWith(EntryUdmux))
            {
                var match = Regex.Match(msg, PatternUdmux);
                if (match.Groups.Count == 3 && match.Groups[2].Value == Current.MachineAddress)
                {
                    // server is behind UDMUX; update the address so the join check still works
                    Current.MachineAddress = match.Groups[1].Value;
                    Logger.WriteLine(id, $"udmux server, real address: {Current.MachineAddress}");
                }
            }
            else if (msg.StartsWith(EntryGameJoined))
            {
                var match = Regex.Match(msg, PatternGameJoined);
                if (match.Groups.Count != 2 || match.Groups[1].Value != Current.MachineAddress)
                {
                    Logger.WriteLine(id, $"server id mismatch, ignoring: {msg}");
                    return;
                }

                InGame = true;
                Current.TimeJoined = DateTime.Now;
                Logger.WriteLine(id, $"joined game ({Current})");
                OnGameJoin?.Invoke(this, EventArgs.Empty);
            }
        }
        else if (InGame && Current.PlaceId != 0)
        {
            // watch for disconnect or teleport
            if (msg.StartsWith(EntryGameDisconnected))
            {
                Logger.WriteLine(id, $"disconnected from game ({Current})");
                Current.TimeLeft = DateTime.Now;
                History.Insert(0, Current);

                InGame = false;
                Current = new GameActivityData();
                OnGameLeave?.Invoke(this, EventArgs.Empty);
            }
            else if (msg.StartsWith(EntryGameTeleporting))
            {
                Logger.WriteLine(id, "teleport initiated");
                _teleportPending = true;
            }
        }
    }
}