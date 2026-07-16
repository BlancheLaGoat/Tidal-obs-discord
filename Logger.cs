namespace TidalNowPlaying;

/// <summary>
/// Journal texte tout simple pour diagnostiquer les problèmes (connexion Discord, etc.)
/// Écrit dans "log.txt" à côté de l'exécutable.
/// </summary>
public static class Logger
{
    private static readonly object Lock = new();
    private static readonly string LogPath = AppPaths.LogPath;

    public static void Log(string message)
    {
        try
        {
            lock (Lock)
            {
                var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // si on ne peut même pas écrire le log, on ne fait rien de plus (pas de crash pour ça)
        }
    }

    public static string GetLogPath() => LogPath;
}
