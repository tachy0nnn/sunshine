using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DiscordRPC;

namespace Sunshine;

/// <summary>
///     Manages Discord Rich Presence while Roblox is running.
/// </summary>
public class DiscordRichPresence : IDisposable
{
    private const string DiscordAppId = "1484662556378398910"; // the app ID
    private const string SmallImageKey = "sunshine";
    private const string SmallImageText = "Sunshine";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders = { { "User-Agent", "Sunshine/1.0" } }
    };

    private readonly DiscordRpcClient _client;
    private readonly GameLogWatcher _watcher;

    private bool _visible = true;

    public DiscordRichPresence(GameLogWatcher watcher)
    {
        const string id = "DiscordRichPresence::ctor";

        _watcher = watcher;
        _client = new DiscordRpcClient(DiscordAppId);

        _client.OnReady += (_, e) => Logger.WriteLine(id, $"ready (user={e.User})");
        _client.OnPresenceUpdate += (_, _) => Logger.WriteLine(id, "presence updated");
        _client.OnError += (_, e) => Logger.WriteLine(id, $"rpc error: {e.Message}");
        _client.OnConnectionEstablished += (_, _) => Logger.WriteLine(id, "connected to discord");
        _client.OnClose += (_, e) => Logger.WriteLine(id, $"connection closed: {e.Reason} ({e.Code})");

        _watcher.OnGameJoin += (_, _) => Task.Run(SetCurrentGame);
        _watcher.OnGameLeave += (_, _) => Task.Run(ClearPresence);

        _client.Initialize();
        Logger.WriteLine(id, "discord rpc client initialised");
    }

    public void Dispose()
    {
        Logger.WriteLine("DiscordRichPresence::Dispose", "disposing");
        _client.ClearPresence();
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    public void SetVisible(bool visible)
    {
        Logger.WriteLine("DiscordRichPresence::SetVisible", visible.ToString());
        _visible = visible;

        if (_visible)
            Task.Run(SetCurrentGame);
        else
            _client.ClearPresence();
    }

    private async Task SetCurrentGame()
    {
        const string id = "DiscordRichPresence::SetCurrentGame";

        if (!_watcher.InGame)
        {
            ClearPresence();
            return;
        }

        var data = _watcher.Current;
        Logger.WriteLine(id, $"setting presence for place {data.PlaceId}");

        // fetch game name + thumbnail from roblox's api
        var gameName = "Roblox";
        var thumbnail = "roblox";
        var creator = "";

        try
        {
            // universe id from place id
            var univJson = await Http.GetStringAsync(
                $"https://apis.roblox.com/universes/v1/places/{data.PlaceId}/universe");

            using var univDoc = JsonDocument.Parse(univJson);
            var universeId = univDoc.RootElement.GetProperty("universeId").GetInt64();

            // game details
            var detailJson = await Http.GetStringAsync(
                $"https://games.roblox.com/v1/games?universeIds={universeId}");

            using var detailDoc = JsonDocument.Parse(detailJson);
            var gameData = detailDoc.RootElement
                .GetProperty("data")[0];

            gameName = gameData.GetProperty("name").GetString() ?? gameName;
            creator = gameData.GetProperty("creator")
                .GetProperty("name").GetString() ?? "";

            // thumbnail
            var thumbJson = await Http.GetStringAsync(
                $"https://thumbnails.roblox.com/v1/games/icons" +
                $"?universeIds={universeId}&returnPolicy=PlaceHolder&size=128x128&format=Png&isCircular=false");

            using var thumbDoc = JsonDocument.Parse(thumbJson);
            var imageUrl = thumbDoc.RootElement
                .GetProperty("data")[0]
                .GetProperty("imageUrl").GetString();

            if (!string.IsNullOrEmpty(imageUrl))
                thumbnail = imageUrl;
        }
        catch (Exception ex)
        {
            Logger.WriteException(id, ex);
            // fall through with defaults; better to show something than nothing
        }

        // guard: player may have left while we were fetching
        if (!_watcher.InGame || _watcher.Current.PlaceId != data.PlaceId)
        {
            Logger.WriteLine(id, "game changed during fetch, aborting");
            return;
        }

        var presence = new RichPresence
        {
            Details = gameName.Length >= 2 ? gameName : gameName + "\u2800\u2800",
            State = string.IsNullOrEmpty(creator) ? null : $"by {creator}",
            Timestamps = new Timestamps { Start = data.TimeJoined.ToUniversalTime() },
            Assets = new Assets
            {
                LargeImageKey = thumbnail,
                LargeImageText = gameName,
                SmallImageKey = SmallImageKey,
                SmallImageText = SmallImageText
            },
            Buttons = BuildButtons(data)
        };

        if (_visible)
            _client.SetPresence(presence);
    }

    private void ClearPresence()
    {
        Logger.WriteLine("DiscordRichPresence::ClearPresence", "clearing");
        _client.ClearPresence();
    }

    private static DiscordRPC.Button[] BuildButtons(GameActivityData data)
    {
        // "join server" deeplink only works for public servers
        return
        [
            new DiscordRPC.Button
            {
                Label = "See game page",
                Url = $"https://www.roblox.com/games/start?=placeId={data.PlaceId}"
            }
        ];
    }
}