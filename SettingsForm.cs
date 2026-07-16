namespace TidalNowPlaying;

/// <summary>
/// Petite fenêtre de réglages construite en code (pas de designer nécessaire).
/// Édite directement config.json puis propose de redémarrer l'appli pour
/// appliquer les changements (nécessaire pour le Client ID Discord et le port).
/// </summary>
public class SettingsForm : Form
{
    private readonly string _configPath;
    private readonly TextBox _discordClientIdBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _imgbbApiKeyBox = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _portBox = new() { Dock = DockStyle.Fill, Minimum = 1024, Maximum = 65535 };
    private readonly NumericUpDown _pollIntervalBox = new() { Dock = DockStyle.Fill, Minimum = 500, Maximum = 10000, Increment = 500 };
    private readonly CheckBox _enableDiscordBox = new() { Text = "Activer le statut Discord", AutoSize = true };
    private readonly CheckBox _enableWidgetBox = new() { Text = "Activer le widget OBS", AutoSize = true };

    public SettingsForm(string configPath, AppConfig current)
    {
        _configPath = configPath;

        Text = "Réglages - Tidal Now Playing";
        Width = 460;
        Height = 380;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        _discordClientIdBox.Text = current.DiscordClientId;
        _imgbbApiKeyBox.Text = current.ImgbbApiKey;
        _portBox.Value = Math.Clamp(current.HttpPort, 1024, 65535);
        _pollIntervalBox.Value = Math.Clamp(current.PollIntervalMs, 500, 10000);
        _enableDiscordBox.Checked = current.EnableDiscordRichPresence;
        _enableWidgetBox.Checked = current.EnableWidgetServer;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 8,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Client ID Discord :", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_discordClientIdBox, 1, 0);

        layout.Controls.Add(new Label { Text = "Clé API ImgBB :", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_imgbbApiKeyBox, 1, 1);

        layout.Controls.Add(new Label { Text = "Port du widget :", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_portBox, 1, 2);

        layout.Controls.Add(new Label { Text = "Intervalle de rafraîchissement (ms) :", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(_pollIntervalBox, 1, 3);

        layout.Controls.Add(_enableDiscordBox, 1, 4);
        layout.Controls.Add(_enableWidgetBox, 1, 5);

        var helpLabel = new Label
        {
            Text = "Client ID Discord : créé sur discord.com/developers/applications\nClé API ImgBB : créée sur api.imgbb.com (pour la pochette d'album)",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 8, 0, 8)
        };
        layout.Controls.Add(helpLabel, 1, 6);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8)
        };
        var cancelButton = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, AutoSize = true };
        var saveButton = new Button { Text = "Enregistrer et redémarrer", AutoSize = true };
        saveButton.Click += OnSave;
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(saveButton);

        Controls.Add(layout);
        Controls.Add(buttonPanel);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var updated = new AppConfig
        {
            DiscordClientId = _discordClientIdBox.Text.Trim(),
            ImgbbApiKey = _imgbbApiKeyBox.Text.Trim(),
            HttpPort = (int)_portBox.Value,
            PollIntervalMs = (int)_pollIntervalBox.Value,
            EnableDiscordRichPresence = _enableDiscordBox.Checked,
            EnableWidgetServer = _enableWidgetBox.Checked
        };

        try
        {
            updated.Save(_configPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'enregistrer les réglages :\n{ex.Message}", "Erreur",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var restart = MessageBox.Show(
            "Réglages enregistrés. Redémarrer l'appli maintenant pour les appliquer ?",
            "Tidal Now Playing", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        DialogResult = DialogResult.OK;
        Close();

        if (restart == DialogResult.Yes)
        {
            var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            System.Diagnostics.Process.Start(exePath);
            Application.Exit();
        }
    }
}
