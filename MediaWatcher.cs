using Windows.Media.Control;
using Windows.Storage.Streams;

namespace TidalNowPlaying;

/// <summary>
/// Surveille les sessions "System Media Transport Controls" de Windows
/// pour trouver celle de TIDAL et en extraire titre/artiste/pochette/progression.
/// C'est la même API qui alimente le widget "lecture en cours" natif de Windows 11.
/// </summary>
public class MediaWatcher : IDisposable
{
    private readonly System.Threading.Timer _pollTimer;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private string _lastTrackKey = "";
    private byte[]? _lastArtBytes;

    public event Action<TrackInfo, byte[]?>? TrackUpdated;

    private readonly int _pollIntervalMs;

    public MediaWatcher(int pollIntervalMs)
    {
        _pollIntervalMs = pollIntervalMs;
        _pollTimer = new System.Threading.Timer(async _ => await PollAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _pollTimer.Change(0, _pollIntervalMs);
    }

    private async Task PollAsync()
    {
        try
        {
            if (_manager == null) return;

            var session = FindBestSession(_manager, out var isDesktop);

            if (session == null && _currentSession != null)
                Logger.Log("MediaWatcher: session perdue (app/onglet fermé ou lecture arrêtée).");
            else if (session != null && _currentSession == null)
                Logger.Log($"MediaWatcher: session détectée -> {session.SourceAppUserModelId}");

            _currentSession = session;

            if (session == null)
            {
                if (_lastTrackKey != "")
                {
                    _lastTrackKey = "";
                    TrackUpdated?.Invoke(new TrackInfo { HasArt = false }, null);
                }
                return;
            }

            var props = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();
            var playbackInfo = session.GetPlaybackInfo();

            var isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            // Windows ne pousse pas forcément une position à jour à chaque appel (certaines apps,
            // dont Tidal, n'envoient une mise à jour de timeline que de temps en temps). On extrapole
            // donc la position réelle à partir du dernier instant connu, pour que la barre de
            // progression avance en continu au lieu de rester figée puis de sauter d'un coup.
            var position = timeline.Position.TotalSeconds;
            if (isPlaying)
            {
                var elapsedSinceUpdate = (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds;
                if (elapsedSinceUpdate > 0)
                    position += elapsedSinceUpdate;
            }

            var duration = timeline.EndTime.TotalSeconds;
            if (duration > 0)
                position = Math.Min(position, duration);

            var info = new TrackInfo
            {
                Title = props.Title ?? "",
                Artist = string.IsNullOrWhiteSpace(props.Artist) ? props.AlbumArtist ?? "" : props.Artist,
                Album = props.AlbumTitle ?? "",
                IsPlaying = isPlaying,
                PositionSeconds = position,
                DurationSeconds = duration,
                IsDesktopSource = isDesktop,
                CapturedAtUtc = DateTime.UtcNow
            };

            byte[]? artBytes = null;
            if (info.TrackKey != _lastTrackKey)
            {
                artBytes = await TryReadThumbnailAsync(props.Thumbnail);
                _lastArtBytes = artBytes;
                _lastTrackKey = info.TrackKey;
            }
            else
            {
                artBytes = _lastArtBytes;
            }

            info.HasArt = artBytes is { Length: > 0 };

            TrackUpdated?.Invoke(info, info.TrackKey == _lastTrackKey ? artBytes : null);
        }
        catch
        {
            // Une session peut disparaître entre deux appels (changement d'app, etc.) - on ignore et on réessaie au prochain tick
        }
    }

    private bool _loggedSessionList;

    private GlobalSystemMediaTransportControlsSession? FindBestSession(GlobalSystemMediaTransportControlsSessionManager manager, out bool isDesktop)
    {
        var sessions = manager.GetSessions();
        var aumids = new List<string>();

        GlobalSystemMediaTransportControlsSession? tidalSession = null;
        GlobalSystemMediaTransportControlsSession? playingFallback = null;
        GlobalSystemMediaTransportControlsSession? anyFallback = null;

        foreach (var session in sessions)
        {
            var aumid = session.SourceAppUserModelId ?? "";
            aumids.Add(aumid);

            if (tidalSession == null && aumid.Contains("tidal", StringComparison.OrdinalIgnoreCase))
            {
                tidalSession = session;
                continue;
            }

            anyFallback ??= session;

            try
            {
                var status = session.GetPlaybackInfo().PlaybackStatus;
                if (playingFallback == null && status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    playingFallback = session;
            }
            catch
            {
                // la session a pu disparaître entre l'énumération et cet appel, on l'ignore simplement
            }
        }

        if (!_loggedSessionList)
        {
            _loggedSessionList = true;
            Logger.Log(aumids.Count == 0
                ? "MediaWatcher: aucune session multimédia active trouvée par Windows."
                : $"MediaWatcher: sessions multimédia détectées -> {string.Join(", ", aumids)}");
        }

        // Priorité : l'appli Tidal desktop (la plus fiable et la plus précise), sinon
        // n'importe quelle lecture active ailleurs (ex. Tidal ou YouTube dans un
        // navigateur), sinon la première session disponible même si elle est en pause.
        if (tidalSession != null)
        {
            isDesktop = true;
            return tidalSession;
        }

        isDesktop = false;
        return playingFallback ?? anyFallback;
    }

    private static async Task<byte[]?> TryReadThumbnailAsync(IRandomAccessStreamReference? thumbnailRef)
    {
        if (thumbnailRef == null) return null;
        try
        {
            using var stream = await thumbnailRef.OpenReadAsync();
            using var dataReader = new DataReader(stream);
            var size = (uint)stream.Size;
            if (size == 0) return null;
            await dataReader.LoadAsync(size);
            var bytes = new byte[size];
            dataReader.ReadBytes(bytes);
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _pollTimer.Dispose();
    }
}
