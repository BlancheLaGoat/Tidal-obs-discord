using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;

namespace TidalNowPlaying;

/// <summary>
/// Met à jour le statut "En train d'écouter" sur Discord à partir des infos Tidal.
/// Nécessite un Client ID Discord (créé gratuitement sur https://discord.com/developers/applications).
/// </summary>
public class DiscordPresenceManager : IDisposable
{
    private DiscordRpcClient? _client;
    private readonly string _clientId;
    private readonly string _imgbbApiKey;
    private string _lastTrackKey = "";
    private DateTime _trackStartedAt;

    private readonly Dictionary<string, string> _imageUrlCache = new();
    private readonly HashSet<string> _uploadsInProgress = new();
    private TrackInfo _lastInfo = new();

    public bool IsEnabled { get; set; }

    public DiscordPresenceManager(string clientId, string imgbbApiKey, bool startEnabled)
    {
        _clientId = clientId;
        _imgbbApiKey = imgbbApiKey;
        IsEnabled = startEnabled;
    }

    public void Start()
    {
        if (string.IsNullOrWhiteSpace(_clientId) || _clientId.Contains("METS_TON_CLIENT_ID"))
        {
            Logger.Log("Discord: aucun Client ID configuré dans config.json -> Rich Presence désactivé.");
            return;
        }

        try
        {
            _client = new DiscordRpcClient(_clientId)
            {
                Logger = new ConsoleLogger { Level = LogLevel.Trace }
            };

            _client.OnReady += (_, msg) =>
                Logger.Log($"Discord: connecté avec succès (utilisateur {msg.User?.Username}).");

            _client.OnConnectionFailed += (_, _) =>
                Logger.Log("Discord: ÉCHEC de connexion. Discord Desktop est-il bien lancé ?");

            _client.OnError += (_, msg) =>
                Logger.Log($"Discord: ERREUR - {msg.Message}");

            _client.OnPresenceUpdate += (_, msg) =>
                Logger.Log($"Discord: présence mise à jour ({msg.Presence?.Details} / {msg.Presence?.State}).");

            _client.Initialize();
            Logger.Log($"Discord: client initialisé avec Client ID {_clientId}.");
        }
        catch (Exception ex)
        {
            Logger.Log($"Discord: EXCEPTION au démarrage - {ex}");
        }
    }

    public void UpdateTrack(TrackInfo info, byte[]? art)
    {
        if (_client == null || !IsEnabled) return;

        if (_client.IsDisposed)
        {
            Logger.Log("Discord: client disposed, impossible de mettre à jour la présence.");
            return;
        }

        if (!info.HasTrack || !info.IsPlaying)
        {
            if (_lastTrackKey != "")
            {
                _client.ClearPresence();
                Logger.Log(info.HasTrack
                    ? "Discord: présence vidée (lecture en pause)."
                    : "Discord: présence vidée (plus de session Tidal).");
                _lastTrackKey = "";
            }
            return;
        }

        var isNewTrack = info.TrackKey != _lastTrackKey;
        if (isNewTrack)
        {
            _trackStartedAt = DateTime.UtcNow.AddSeconds(-info.PositionSeconds);
            _lastTrackKey = info.TrackKey;
        }
        _lastInfo = info;

        // Pochette : on utilise l'URL en cache si on l'a déjà, sinon le logo par défaut
        // en attendant que l'upload (déclenché ci-dessous) se termine.
        _imageUrlCache.TryGetValue(info.TrackKey, out var cachedArtUrl);

        SendPresence(info, cachedArtUrl);

        if (isNewTrack && art is { Length: > 0 } && !_imageUrlCache.ContainsKey(info.TrackKey))
        {
            TryUploadArtAsync(info.TrackKey, art);
        }
    }

    private async void TryUploadArtAsync(string trackKey, byte[] art)
    {
        if (!_uploadsInProgress.Add(trackKey)) return;

        var url = await ImgBbUploader.UploadAsync(art, _imgbbApiKey);

        _uploadsInProgress.Remove(trackKey);

        if (url == null) return;

        _imageUrlCache[trackKey] = url;

        // Si on écoute toujours le même morceau, on republie la présence avec la vraie pochette.
        if (trackKey == _lastTrackKey)
        {
            SendPresence(_lastInfo, url);
        }
    }

    private void SendPresence(TrackInfo info, string? artUrl)
    {
        if (_client == null) return;

        var presence = new RichPresence
        {
            Details = Truncate(info.Title, 128),
            State = Truncate(string.IsNullOrWhiteSpace(info.Artist) ? "Tidal" : $"par {info.Artist}", 128),
            Assets = new Assets
            {
                // Si on a une URL de pochette (Imgur), on l'utilise directement (Discord accepte les URLs
                // externes pour la grande image). Sinon on retombe sur l'asset fixe "tidal_logo".
                LargeImageKey = artUrl ?? "tidal_logo",
                LargeImageText = string.IsNullOrWhiteSpace(info.Album) ? "TIDAL" : info.Album,
                SmallImageKey = info.IsPlaying ? "play_icon" : "pause_icon",
                SmallImageText = info.IsPlaying ? "En lecture" : "En pause"
            }
        };

        if (info.IsPlaying && info.DurationSeconds > 0)
        {
            presence.Timestamps = new Timestamps
            {
                Start = _trackStartedAt,
                End = _trackStartedAt.AddSeconds(info.DurationSeconds)
            };
        }

        try
        {
            _client.SetPresence(presence);
            Logger.Log($"Discord: SetPresence envoyé pour '{info.Artist} - {info.Title}' (pochette: {(artUrl != null ? "ImgBB" : "logo par défaut")}).");
        }
        catch (Exception ex)
        {
            Logger.Log($"Discord: EXCEPTION lors de SetPresence - {ex}");
        }
    }

    public void Clear()
    {
        _client?.ClearPresence();
        _lastTrackKey = "";
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "‎" : (s.Length <= max ? s : s.Substring(0, max));

    public void Dispose()
    {
        _client?.Dispose();
    }
}
