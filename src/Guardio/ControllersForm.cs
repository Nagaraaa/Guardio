using Guardio.Ui;

namespace Guardio;

/// <summary>Lets the user mark which audio outputs belong to a controller.</summary>
public sealed class ControllersForm : Form
{
    public ControllersForm(GuardService guard)
    {
        SuspendLayout();
        AutoScaleDimensions = new(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Manettes"; Font = new("Segoe UI", 9.75f);
        ClientSize = new(440, 420); MinimumSize = new(380, 300);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        MinimizeBox = false; MaximizeBox = false;

        var rows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new(0) };
        rows.ColumnStyles.Add(new(SizeType.Percent, 100)); rows.ColumnStyles.Add(new(SizeType.AutoSize));
        // Unplugged outputs are listed only if they are, or were marked as, a controller.
        var outputs = guard.Outputs.Where(e => e.Active || e.IsController || guard.ControllerOverrides.ContainsKey(e.Id))
            .OrderByDescending(e => e.Active).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase);
        foreach (var e in outputs)
        {
            var name = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new(0, 6, 0, 6), Anchor = AnchorStyles.Left };
            name.Controls.Add(new Label { Text = e.Name, AutoSize = true, Margin = new(0) });
            name.Controls.Add(new MutedLabel { Text = e.Active ? "Branchée" : "Débranchée", Margin = new(0) });
            var toggle = new ToggleSwitch { Checked = e.IsController, Anchor = AnchorStyles.Right, AccessibleName = "Manette : " + e.Name };
            toggle.CheckedChanged += (_, _) => guard.SetController(e.Id, toggle.Checked);
            rows.Controls.Add(name); rows.Controls.Add(toggle);
        }
        var card = new Card { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Current.Surface };
        card.Controls.Add(rows);

        var done = new FlatButton { Text = "Terminé", Primary = true, Anchor = AnchorStyles.Right, Margin = new(0, 12, 0, 0), DialogResult = DialogResult.OK };
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(20, 16, 20, 16), ColumnCount = 1 };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.AutoSize));
        root.Controls.Add(new MutedLabel { Text = "Branche ta manette pour la voir dans la liste.", Margin = new(0, 0, 0, 12) });
        root.Controls.Add(card); root.Controls.Add(done);
        Controls.Add(root);
        AcceptButton = done; CancelButton = done;
        ResumeLayout(false); PerformLayout();
        HandleCreated += (_, _) => Theme.Apply(this);
    }
}
