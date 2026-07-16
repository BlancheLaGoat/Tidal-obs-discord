using System.Diagnostics;

namespace TidalNowPlaying;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly MediaWatcher _mediaWatcher;
    private readonly DiscordPresenceManager _discord;
    private readonly WidgetServer? _widgetServer;
    private readonly AppConfig _config;
    private readonly string _configPath;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _discordToggleItem;
    private readonly ToolStripMenuItem _autoStartToggleItem;

    public TrayApplicationContext()
    {
        var baseDir = AppContext.BaseDirectory;
        _configPath = Path.Combine(baseDir, "config.json");
        _config = AppConfig.Load(_configPath);

        _mediaWatcher = new MediaWatcher(_config.PollIntervalMs);
        _discord = new DiscordPresenceManager(_config.DiscordClientId, _config.ImgbbApiKey, _config.EnableDiscordRichPresence);

        if (_config.EnableWidgetServer)
        {
            _widgetServer = new WidgetServer(_config.HttpPort, Path.Combine(baseDir, "wwwroot"));
        }

        _statusItem = new ToolStripMenuItem("En attente de lecture Tidal…") { Enabled = false };
        _discordToggleItem = new ToolStripMenuItem("Statut Discord activé", null, OnToggleDiscord) { Checked = _config.EnableDiscordRichPresence };
        _autoStartToggleItem = new ToolStripMenuItem("Démarrer avec Windows", null, OnToggleAutoStart) { Checked = AutoStartManager.IsEnabled() };

        var settingsItem = new ToolStripMenuItem("Réglages…", null, OnOpenSettings);
        var openWidgetItem = new ToolStripMenuItem("Ouvrir le widget dans le navigateur", null, OnOpenWidget);
        var copyUrlItem = new ToolStripMenuItem("Copier l'URL du widget (pour OBS)", null, OnCopyWidgetUrl);
        var openLogItem = new ToolStripMenuItem("Voir le journal (log.txt)", null, OnOpenLog);
        var quitItem = new ToolStripMenuItem("Quitter", null, OnQuit);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_discordToggleItem);
        menu.Items.Add(_autoStartToggleItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(openWidgetItem);
        menu.Items.Add(copyUrlItem);
        menu.Items.Add(openLogItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Tidal Now Playing",
            Visible = true,
            ContextMenuStrip = menu
        };

        _mediaWatcher.TrackUpdated += OnTrackUpdated;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        Logger.Log("Démarrage de TidalNowPlaying...");
        Logger.Log($"Discord Rich Presence activé dans config.json: {_config.EnableDiscordRichPresence}");
        Logger.Log($"Widget serveur activé: {_config.EnableWidgetServer} (port {_config.HttpPort})");
        _discord.Start();
        _widgetServer?.Start();
        await _mediaWatcher.StartAsync();
        Logger.Log("MediaWatcher démarré, en attente d'une session Tidal...");
    }

    private void OnTrackUpdated(TrackInfo info, byte[]? art)
    {
        // Les callbacks WinRT n'arrivent pas forcément sur le thread UI
        if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
        {
            _trayIcon.ContextMenuStrip.Invoke(() => ApplyUpdate(info, art));
        }
        else
        {
            ApplyUpdate(info, art);
        }
    }

    private void ApplyUpdate(TrackInfo info, byte[]? art)
    {
        _statusItem.Text = info.HasTrack
            ? $"{(info.IsPlaying ? "▶" : "⏸")} {info.Artist} - {info.Title}"
            : "En attente de lecture Tidal…";

        _trayIcon.Text = info.HasTrack
            ? Truncate($"{info.Artist} - {info.Title}", 63)
            : "Tidal Now Playing";

        _discord.UpdateTrack(info, art);
        _widgetServer?.UpdateTrack(info, art);
    }

    private void OnToggleDiscord(object? sender, EventArgs e)
    {
        _discordToggleItem.Checked = !_discordToggleItem.Checked;
        _discord.IsEnabled = _discordToggleItem.Checked;
        if (!_discord.IsEnabled) _discord.Clear();
    }

    private void OnOpenWidget(object? sender, EventArgs e)
    {
        if (_widgetServer == null) return;
        Process.Start(new ProcessStartInfo($"http://localhost:{_config.HttpPort}/") { UseShellExecute = true });
    }

    private void OnCopyWidgetUrl(object? sender, EventArgs e)
    {
        if (_widgetServer == null) return;
        Clipboard.SetText($"http://localhost:{_config.HttpPort}/");
        MessageBox.Show("URL du widget copiée dans le presse-papiers.\nColle-la dans une Source Navigateur OBS.",
            "Tidal Now Playing", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnToggleAutoStart(object? sender, EventArgs e)
    {
        _autoStartToggleItem.Checked = !_autoStartToggleItem.Checked;
        AutoStartManager.SetEnabled(_autoStartToggleItem.Checked);
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_configPath, _config);
        form.ShowDialog();
    }

    private void OnOpenLog(object? sender, EventArgs e)
    {
        var path = Logger.GetLogPath();
        if (!File.Exists(path))
        {
            MessageBox.Show("Aucun log pour l'instant.", "Tidal Now Playing");
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _discord.Clear();
        _mediaWatcher.Dispose();
        _discord.Dispose();
        _widgetServer?.Dispose();
        Application.Exit();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
}
