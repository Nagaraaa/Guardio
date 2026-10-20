using System.Text.Json;

namespace GardeAudio;

public sealed class MainForm : Form
{
    private readonly Label status = new() { AutoSize = true, Font = new("Segoe UI", 12, FontStyle.Bold) };
    private readonly Label controller = new() { AutoSize = true };
    private readonly Label output = new() { AutoSize = true, MaximumSize = new(570, 0) };
    private readonly ComboBox fallback = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 550 };
    private readonly ComboBox manualOutput = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 415 };
    private readonly ListBox journal = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false };
    private readonly CheckBox enabled = new() { Text = "Protéger les branchements de la DualSense", AutoSize = true, Checked = true };
    private readonly NotifyIcon tray = new() { Icon = SystemIcons.Shield, Text = "GardeAudio — DualSense", Visible = true };
    private readonly string dataDir;
    private GuardService? guard;
    private bool refreshing;
    private string shownSignature = "";
    private readonly ToolStripMenuItem trayEnable = new("Protection active") { Checked = true, CheckOnClick = true };

    public MainForm(string dataDir)
    {
        this.dataDir = dataDir;
        Text = "GardeAudio · DualSense · 0.1.0";
        Font = new("Segoe UI", 10); BackColor = Color.FromArgb(248, 249, 251);
        ClientSize = new(620, 545); MinimumSize = new(600, 560);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(22), ColumnCount = 1, RowCount = 12 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        foreach (int height in new[] { 42, 31, 40, 32, 27, 38, 30, 42, 48, 25 })
            layout.RowStyles.Add(new(SizeType.Absolute, height));
        layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 34));
        layout.Controls.Add(status, 0, 0); layout.Controls.Add(controller, 0, 1); layout.Controls.Add(output, 0, 2);
        layout.Controls.Add(enabled, 0, 3);
        layout.Controls.Add(new Label { Text = "Sortie de secours si la précédente n’est plus disponible", AutoSize = true }, 0, 4);
        layout.Controls.Add(fallback, 0, 5);
        layout.Controls.Add(new Label { Text = "Changer volontairement de sortie", AutoSize = true }, 0, 6);
        var manualRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var apply = new Button { Text = "Utiliser", AutoSize = true, Height = 31 };
        manualRow.Controls.Add(manualOutput); manualRow.Controls.Add(apply); layout.Controls.Add(manualRow, 0, 7);
        layout.Controls.Add(new Label { Text = "Casque et enceintes restent libres. Pour choisir la manette pendant\nles 2 s du branchement, sélectionne-la ici puis clique sur Utiliser.", AutoSize = true, ForeColor = Color.DimGray }, 0, 8);
        layout.Controls.Add(new Label { Text = "Activité", AutoSize = true }, 0, 9);
        layout.Controls.Add(journal, 0, 10);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var quit = new Button { Text = "Quitter", AutoSize = true };
        var hide = new Button { Text = "Réduire près de l’horloge", AutoSize = true };
        footer.Controls.Add(quit); footer.Controls.Add(hide); layout.Controls.Add(footer, 0, 11);
        Controls.Add(layout);
        enabled.CheckedChanged += (_, _) => { if (!refreshing && guard is not null) { guard.Enable(enabled.Checked); SaveSettings(); } };
        fallback.SelectedIndexChanged += (_, _) => { if (!refreshing && guard is not null) { guard.FallbackId = (fallback.SelectedItem as Endpoint)?.Id; SaveSettings(); } };
        apply.Click += (_, _) => { if (manualOutput.SelectedItem is Endpoint e) Run(() => guard!.SelectManually(e.Id)); };
        quit.Click += (_, _) => Close(); hide.Click += (_, _) => Hide();
        tray.DoubleClick += (_, _) => OpenWindow();
        var menu = new ContextMenuStrip();
        menu.Items.Add("Ouvrir GardeAudio", null, (_, _) => OpenWindow());
        menu.Items.Add(trayEnable);
        trayEnable.CheckedChanged += (_, _) => { if (!refreshing) enabled.Checked = trayEnable.Checked; };
        menu.Items.Add("Quitter", null, (_, _) => Close()); tray.ContextMenuStrip = menu;
        Shown += (_, _) => StartGuard();
        FormClosed += (_, _) => { tray.Visible = false; tray.Dispose(); guard?.Dispose(); };
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
    }

    private void StartGuard()
    {
        Run(() =>
        {
            guard = new GuardService(this, new NativeAudio(), dataDir);
            guard.Changed += RefreshView;
            var saved = LoadSettings();
            guard.FallbackId = guard.Outputs.FirstOrDefault(e => e.Active && !e.IsDualSense && e.Id == saved.FallbackId)?.Id
                ?? guard.Outputs.FirstOrDefault(e => e.Active && !e.IsDualSense && e.Name.Contains("Logitech PRO X", StringComparison.OrdinalIgnoreCase))?.Id
                ?? guard.Outputs.FirstOrDefault(e => e.Active && !e.IsDualSense)?.Id;
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
            enabled.Checked = guard.Enabled; trayEnable.Checked = guard.Enabled;
            var duals = guard.Outputs.Where(e => e.Active && e.IsDualSense).ToArray();
            status.Text = guard.Enabled ? "Protection active au branchement" : "Protection en pause";
            status.ForeColor = guard.Enabled ? Color.FromArgb(24, 105, 73) : Color.DarkOrange;
            controller.Text = duals.Length > 0 ? "DualSense détectée · sortie audio disponible" : "DualSense débranchée · en attente";
            output.Text = "Son actuel : " + guard.CurrentName;
            var active = guard.Outputs.Where(e => e.Active).ToList();
            var signature = string.Join("|", active.Select(e => e.Id + e.Name));
            if (signature != shownSignature)
            {
                var manualId = (manualOutput.SelectedItem as Endpoint)?.Id;
                fallback.Items.Clear(); manualOutput.Items.Clear();
                foreach (var e in active)
                {
                    if (!e.IsDualSense) fallback.Items.Add(e);
                    manualOutput.Items.Add(e);
                }
                manualOutput.SelectedItem = active.FirstOrDefault(e => e.Id == manualId) ?? active.FirstOrDefault();
                shownSignature = signature;
            }
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

    private record Settings(string? FallbackId = null, bool Enabled = true);
    private Settings LoadSettings()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(dataDir, "reglages.json"))) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException) { return new(); }
    }
    private void SaveSettings()
    {
        if (guard is null) return;
        Run(() => File.WriteAllText(Path.Combine(dataDir, "reglages.json"),
            JsonSerializer.Serialize(new Settings(guard.FallbackId, guard.Enabled), GuardService.JsonOptions)));
    }
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "GardeAudio", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void OpenWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
}
