namespace TidalNowPlaying;

/// <summary>
/// Laisse passer les infos de lecture telles quelles, mais si la lecture est en
/// pause (ou arrêtée) pendant plus de <see cref="TimeoutSeconds"/> secondes,
/// renvoie un TrackInfo vide à la place - ce qui fait que Discord et le widget
/// se désactivent, jusqu'à ce que la musique reprenne.
/// </summary>
public class IdleGate
{
    public double TimeoutSeconds { get; set; } = 60;

    private DateTime? _idleSinceUtc;

    public TrackInfo Apply(TrackInfo info)
    {
        if (info.IsPlaying || TimeoutSeconds <= 0)
        {
            _idleSinceUtc = null;
            return info;
        }

        _idleSinceUtc ??= DateTime.UtcNow;

        var idleForSeconds = (DateTime.UtcNow - _idleSinceUtc.Value).TotalSeconds;
        if (idleForSeconds >= TimeoutSeconds)
        {
            return new TrackInfo(); // Title vide -> HasTrack = false -> tout se vide côté Discord/widget
        }

        return info;
    }
}
