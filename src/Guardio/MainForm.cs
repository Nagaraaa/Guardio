using System.Text.Json;
using Guardio.Ui;

namespace Guardio;

public sealed class MainForm : Form
{
    private readonly Label status = new() { AutoSize = true, Margin = new(0, 2, 0, 0) };
    private readonly ToggleSwitch enabled = new() { Checked = true, Anchor = AnchorStyles.Right, AccessibleName = "Protection" };
    private readonly FlatCombo output = new() { AccessibleName = "Sortie audio" };
    private readonly FlatCombo fallback = new() { AccessibleName = "Sortie de secours" };
    private readonly FlowLayoutPanel controllers = new()
    { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Top, Margin = new(0) };
    private readonly ActivityList journal = new();
    private readonly NotifyIcon tray = new() { Text = "Guardio", Visible = true };
    private readonly ToolStripMenuItem trayEnable = new("Protection active") { Checked = true, CheckOnClick = true };
    private readonly ToggleSwitch autoStart = new() { Anchor = AnchorStyles.Right, AccessibleName = "Lancer avec Windows" };
    private readonly ToolTip tips = new();
    private readonly string dataDir;
    private GuardService? guard;
    private bool refreshing, startHidden, quitting, trayHintShown;
    private string shownSignature = "";

    public MainForm(string dataDir, bool startHidden = false)
    {
        this.dataDir = dataDir; this.startHidden = startHidden;
        SuspendLayout();
        AutoScaleDimensions = new(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Guardio"; Font = new("Segoe UI", 9.75f);
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;
        tray.Icon = Icon;
        ClientSize = new(480, 650); MinimumSize = new(440, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(20, 18, 20, 14), ColumnCount = 1 };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.Controls.Add(Header());
        root.Controls.Add(AudioCard());
        root.Controls.Add(ControllersCard());
        root.Controls.Add(StartupCard());
        var activity = ActivityCard();
        root.Controls.Add(activity);
        root.Controls.Add(Footer());
        for (int i = 0; i < root.Controls.Count; i++)
            root.RowStyles.Add(root.Controls[i] == activity ? new(SizeType.Percent, 100) : new(SizeType.AutoSize));
        Controls.Add(root);
        ResumeLayout(false); PerformLayout();

        enabled.CheckedChanged += (_, _) => { if (!refreshing && guard is not null) { guard.Enable(enabled.Checked); SaveSettings(); } };
        output.SelectionChangeCommitted += (_, _) => { if (output.SelectedItem is Endpoint e) Run(() => guard!.SelectManually(e.Id)); };
        fallback.SelectedIndexChanged += (_, _) => { if (!refreshing && guard is not null) { guard.FallbackId = (fallback.SelectedItem as Endpoint)?.Id; SaveSettings(); } };
        tray.DoubleClick += (_, _) => OpenWindow();
        var menu = new ContextMenuStrip();
        menu.Items.Add("Ouvrir Guardio", null, (_, _) => OpenWindow());
        menu.Items.Add(trayEnable);
        trayEnable.CheckedChanged += (_, _) => { if (!refreshing) enabled.Checked = trayEnable.Checked; };
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => Quit()); tray.ContextMenuStrip = menu;
        Theme.Changed += OnThemeChanged;
        // The guard starts with the window handle, so it also runs when started hidden.
        HandleCreated += (_, _) => { Theme.Apply(this); if (guard is null) StartGuard(); };
        FormClosing += OnClosing;
        FormClosed += (_, _) => { Theme.Changed -= OnThemeChanged; tray.Visible = false; tray.Dispose(); guard?.Dispose(); };
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
    }

    protected override void SetVisibleCore(bool value)
    {
        // Started with Windows: stay in the notification area until opened.
        if (startHidden && value)
        {
            startHidden = false; value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        // The close button keeps protection running; "Quitter" really exits.
        if (quitting || e.CloseReason != CloseReason.UserClosing) return;
        e.Cancel = true; Hide();
        if (trayHintShown) return;
        trayHintShown = true;
        tray.ShowBalloonTip(4000, "Guardio reste actif", "Clic droit sur l’icône puis Quitter pour l’arrêter.", ToolTipIcon.None);
    }

    private void Quit() { quitting = true; Close(); }

    private Control Header()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = new(0, 0, 0, 16) };
        header.ColumnStyles.Add(new(SizeType.AutoSize)); header.ColumnStyles.Add(new(SizeType.Percent, 100)); header.ColumnStyles.Add(new(SizeType.AutoSize));
        var titles = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new(0), Anchor = AnchorStyles.Left };
        titles.Controls.Add(new Label { Text = "Guardio", AutoSize = true, Font = new("Segoe UI Semibold", 15f), Margin = new(0) });
        titles.Controls.Add(status);
        header.Controls.Add(new IconBadge(Theme.Headphones) { Anchor = AnchorStyles.Left }, 0, 0);
        header.Controls.Add(titles, 1, 0);
        header.Controls.Add(enabled, 2, 0);
        tips.SetToolTip(enabled, "Rétablir la sortie audio quand Windows bascule sur une manette qu’on vient de brancher.");
        return header;
    }

    private Control AudioCard()
    {
        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new(0, 6, 0, 0) };
        grid.ColumnStyles.Add(new(SizeType.AutoSize)); grid.ColumnStyles.Add(new(SizeType.Percent, 100));
        grid.Controls.Add(new Label { Text = "Sortie", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new(0, 0, 16, 0) }, 0, 0);
        grid.Controls.Add(output, 1, 0);
        var fallbackLabel = new Label { Text = "Secours", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new(0, 0, 16, 0) };
        grid.Controls.Add(fallbackLabel, 0, 1);
        grid.Controls.Add(fallback, 1, 1);
        tips.SetToolTip(fallbackLabel, "Utilisée si la sortie précédente n’est plus disponible.");
        tips.SetToolTip(fallback, "Utilisée si la sortie précédente n’est plus disponible.");
        return MakeCard(Theme.Speaker, "Audio", null, grid);
    }

    private Control ControllersCard()
    {
        var manage = new FlatButton { Text = "Gérer", MinimumSize = new(0, 28), Padding = new(10, 2, 10, 2), Anchor = AnchorStyles.Right };
        manage.Click += (_, _) => ManageControllers();
        tips.SetToolTip(manage, "Choisir quelles sorties audio sont des manettes.");
        return MakeCard(Theme.Gamepad, "Manettes", manage, controllers);
    }

    private Control StartupCard()
    {
        try { AutoStart.Refresh(); autoStart.Checked = AutoStart.Enabled; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { autoStart.Enabled = false; }
        autoStart.CheckedChanged += (_, _) =>
        {
            if (refreshing) return;
            Run(() => AutoStart.Enabled = autoStart.Checked);
            refreshing = true; autoStart.Checked = AutoStart.Enabled; refreshing = false;
        };
        tips.SetToolTip(autoStart, "Démarrer Guardio dans la zone de notification à l’ouverture de session.");
        return MakeCard(Theme.Power, "Lancer avec Windows", autoStart, null);
    }

    private Control ActivityCard()
    {
        var card = MakeCard(Theme.History, "Activité", null, journal);
        card.AutoSize = false; card.Dock = DockStyle.Fill; card.MinimumSize = new(0, 120);
        return card;
    }

    private Control Footer()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new(0) };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100)); footer.ColumnStyles.Add(new(SizeType.AutoSize));
        var quit = new FlatButton { Text = "Quitter", Margin = new(0) };
        tips.SetToolTip(quit, "Arrêter Guardio. La croix le garde dans la zone de notification.");
        quit.Click += (_, _) => Quit();
        footer.Controls.Add(new MutedLabel { Text = "v" + Program.Version, Anchor = AnchorStyles.Left }, 0, 0);
        footer.Controls.Add(quit, 1, 0);
        return footer;
    }

    private static Card MakeCard(string glyph, string title, Control? action, Control? body)
    {
        var card = new Card { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Current.Surface };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, Margin = new(0) };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        var head = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = new(0, 0, 0, body is null ? 0 : 6) };
        head.ColumnStyles.Add(new(SizeType.AutoSize)); head.ColumnStyles.Add(new(SizeType.Percent, 100)); head.ColumnStyles.Add(new(SizeType.AutoSize));
        head.Controls.Add(new Glyph(glyph) { Anchor = AnchorStyles.Left }, 0, 0);
        head.Controls.Add(new Label { Text = title, AutoSize = true, Font = new("Segoe UI Semibold", 10.5f), Anchor = AnchorStyles.Left, Margin = new(0) }, 1, 0);
        if (action is not null) head.Controls.Add(action, 2, 0);
        layout.Controls.Add(head, 0, 0);
        if (body is not null) layout.Controls.Add(body, 0, 1);
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100));
        card.Controls.Add(layout);
        return card;
    }

    private void StartGuard()
    {
        Run(() =>
        {
            var saved = LoadSettings();
            guard = new GuardService(this, new NativeAudio(), dataDir, saved.Controllers);
            guard.Changed += RefreshView;
            guard.FallbackId = guard.Outputs.FirstOrDefault(e => e.Active && !e.IsController && e.Id == saved.FallbackId)?.Id
                ?? guard.Outputs.FirstOrDefault(e => e.Active && !e.IsController)?.Id;
            if (!saved.Enabled) guard.Enable(false);
            RefreshView(); guard.WriteStatus();
        });
    }

    private void RefreshView()
    {
        if (guard is null || IsDisposed) return;
        refreshing = true;
        try
        {
            var t = Theme.Current;
            enabled.Checked = guard.Enabled; trayEnable.Checked = guard.Enabled;
            status.Text = guard.Enabled ? "●  Protection active" : "●  En pause";
            status.ForeColor = guard.Enabled ? t.Success : t.Warning;
            tray.Text = guard.Enabled ? "Guardio · protection active" : "Guardio · en pause";
            var active = guard.Outputs.Where(e => e.Active).ToList();
            var signature = string.Join("|", active.Select(e => e.Id + e.Name + e.IsController));
            if (signature != shownSignature)
            {
                output.Items.Clear(); fallback.Items.Clear();
                foreach (var e in active)
                {
                    output.Items.Add(e);
                    if (!e.IsController) fallback.Items.Add(e);
                }
                ShowControllers(active.Where(e => e.IsController).ToList());
                shownSignature = signature;
            }
            output.SelectedItem = output.Items.Cast<Endpoint>().FirstOrDefault(e => e.Id == guard.CurrentId);
            fallback.SelectedItem = fallback.Items.Cast<Endpoint>().FirstOrDefault(e => e.Id == guard.FallbackId);
            if (journal.Items.Count != guard.Log.Count || (journal.Items.Count > 0 && (string?)journal.Items[^1] != guard.Log.LastOrDefault()))
            {
                journal.BeginUpdate(); journal.Items.Clear(); journal.Items.AddRange(guard.Log.Cast<object>().ToArray());
                if (journal.Items.Count > 0) journal.TopIndex = journal.Items.Count - 1;
                journal.EndUpdate();
            }
        }
        finally { refreshing = false; }
    }

    private void ShowControllers(List<Endpoint> connected)
    {
        controllers.SuspendLayout();
        foreach (Control c in controllers.Controls.Cast<Control>().ToArray()) c.Dispose();
        if (connected.Count == 0)
            controllers.Controls.Add(new MutedLabel { Text = "Aucune manette branchée", Margin = new(0, 2, 0, 2) });
        foreach (var e in connected)
            controllers.Controls.Add(new Label { Text = e.Name, AutoSize = true, Margin = new(0, 2, 0, 2) });
        controllers.ResumeLayout();
    }

    private void ManageControllers()
    {
        if (guard is null) return;
        using var dialog = new ControllersForm(guard);
        dialog.ShowDialog(this);
        SaveSettings();
    }

    private void OnThemeChanged()
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() => { Theme.Apply(this); RefreshView(); });
    }

    private record Settings(string? FallbackId = null, bool Enabled = true, Dictionary<string, bool>? Controllers = null);
    private Settings LoadSettings()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(dataDir, "reglages.json"))) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException) { return new(); }
    }
    private void SaveSettings()
    {
        if (guard is null) return;
        Run(() => File.WriteAllText(Path.Combine(dataDir, "reglages.json"), JsonSerializer.Serialize(
            new Settings(guard.FallbackId, guard.Enabled, guard.ControllerOverrides.ToDictionary()), GuardService.JsonOptions)));
    }
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Guardio", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void OpenWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
}
