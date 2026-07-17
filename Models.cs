using System.Text.Json;
using System.Text.Json.Serialization;

namespace TidalNowPlaying;

public class TrackInfo
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public bool IsPlaying { get; set; }
    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public bool HasArt { get; set; }

    // true si la session vient de l'appli Tidal desktop, false si elle vient d'un
    // navigateur ou d'une autre source détectée en fallback (voir MediaWatcher).
    public bool IsDesktopSource { get; set; } = true;

    // Instant (UTC) auquel PositionSeconds a été mesuré. Sert à extrapoler la
    // position en temps réel côté widget, car Windows ne renvoie pas toujours
    // une position parfaitement à jour à chaque appel.
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

    // Utilisé pour savoir si on doit régénérer la pochette / republier sur Discord
    public string TrackKey => $"{Artist}::{Title}::{Album}";

    public bool HasTrack => !string.IsNullOrWhiteSpace(Title);
}

public class AppConfig
{
    [JsonPropertyName("discordClientId")]
    public string DiscordClientId { get; set; } = "";

    [JsonPropertyName("imgbbApiKey")]
    public string ImgbbApiKey { get; set; } = "";

    [JsonPropertyName("httpPort")]
    public int HttpPort { get; set; } = 5177;

    [JsonPropertyName("enableDiscordRichPresence")]
    public bool EnableDiscordRichPresence { get; set; } = true;

    [JsonPropertyName("discordDesktopOnly")]
    public bool DiscordDesktopOnly { get; set; } = true;

    [JsonPropertyName("enableWidgetServer")]
    public bool EnableWidgetServer { get; set; } = true;

    [JsonPropertyName("pollIntervalMs")]
    public int PollIntervalMs { get; set; } = 1500;

    public static AppConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // config invalide -> on retombe sur les valeurs par défaut
        }
        return new AppConfig();
    }

    public void Save(string path)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(this, options));
    }
}
