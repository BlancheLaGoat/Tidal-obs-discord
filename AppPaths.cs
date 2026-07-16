namespace TidalNowPlaying;

/// <summary>
/// Fournit un dossier stable dans %AppData% pour stocker la config et les logs,
/// indépendamment de l'endroit où se trouve l'exe (important pour les builds
/// "fichier unique" où le dossier de l'exe n'est pas un endroit fiable où écrire).
/// </summary>
public static class AppPaths
{
    private static readonly Lazy<string> DataDir = new(() =>
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TidalNowPlaying");
        Directory.CreateDirectory(dir);
        return dir;
    });

    public static string GetDataDir() => DataDir.Value;

    public static string ConfigPath => Path.Combine(GetDataDir(), "config.json");

    public static string LogPath => Path.Combine(GetDataDir(), "log.txt");
}
