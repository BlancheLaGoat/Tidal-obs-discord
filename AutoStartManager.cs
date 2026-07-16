using Microsoft.Win32;

namespace TidalNowPlaying;

/// <summary>
/// Active/désactive le démarrage automatique avec Windows en ajoutant/retirant
/// une entrée dans la clé de registre Run de l'utilisateur courant (pas besoin
/// de droits administrateur).
/// </summary>
public static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TidalNowPlaying";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                             ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue(ValueName, $"\"{exePath}\"");
                Logger.Log($"AutoStart: activé ({exePath}).");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                Logger.Log("AutoStart: désactivé.");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"AutoStart: EXCEPTION - {ex.Message}");
        }
    }
}
