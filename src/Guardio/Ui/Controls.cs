using System.Drawing.Drawing2D;

namespace Guardio.Ui;

/// <summary>Rounded surface that groups related settings.</summary>
internal sealed class Card : Panel
{
    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Padding = new(18, 14, 18, 16); Margin = new(0, 0, 0, 12);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var t = Theme.Current;
        e.Graphics.Clear(Parent?.BackColor ?? t.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(10));
        using var fill = new SolidBrush(t.Surface); e.Graphics.FillPath(fill, path);
        using var pen = new Pen(t.Border); e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>Label drawn in the secondary text color.</summary>
internal sealed class MutedLabel : Label
{
    public MutedLabel() { AutoSize = true; ForeColor = Theme.Current.Muted; }
}

/// <summary>Single icon glyph from Segoe Fluent Icons / MDL2 Assets.</summary>
internal sealed class Glyph : Label
{
    public Glyph(string glyph, float size = 13)
    {
        Text = glyph; Font = Theme.IconFont(size); AutoSize = true; ForeColor = Theme.Current.Accent;
        UseMnemonic = false; Margin = new(0, 2, 8, 0);
    }
}

/// <summary>Rounded flat button: accent (primary) or subtle.</summary>
internal sealed class FlatButton : Button
{
    private bool hover, pressed;
    public bool Primary { get; init; }

    public FlatButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new(12, 5, 12, 5); MinimumSize = new(0, 34); Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var t = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? t.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var fill = Primary ? t.Accent : t.Subtle;
        if (!Enabled) fill = Theme.Blend(fill, Parent?.BackColor ?? t.Surface, 0.5f);
        else if (pressed) fill = Theme.Blend(fill, t.Text, 0.15f);
        else if (hover) fill = Theme.Blend(fill, t.Text, 0.07f);
        using (var path = Theme.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(6)))
        using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
        if (Focused && ShowFocusCues)
        {
            using var focus = Theme.Rounded(new RectangleF(2, 2, Width - 5, Height - 5), LogicalToDeviceUnits(5));
            using var pen = new Pen(Primary ? t.AccentText : t.Accent); g.DrawPath(pen, focus);
        }
        var color = Primary ? t.AccentText : t.Text;
        if (!Enabled) color = t.Muted;
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>On/off switch (a CheckBox, so keyboard and accessibility come for free).</summary>
internal sealed class ToggleSwitch : CheckBox
{
    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AutoSize = false; Size = new(44, 24); Cursor = Cursors.Hand; Margin = new(8, 0, 0, 0);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var t = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? t.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new RectangleF(1, 1, Width - 3, Height - 3);
        using (var path = Theme.Rounded(track, track.Height / 2))
        {
            if (Checked) { using var b = new SolidBrush(t.Accent); g.FillPath(b, path); }
            else { using var pen = new Pen(t.Muted, 1.5f); g.DrawPath(pen, path); }
        }
        float knob = track.Height - (Checked ? 8 : 10);
        float x = Checked ? track.Right - knob - 4 : track.Left + 5;
        using (var b = new SolidBrush(Checked ? t.AccentText : t.Muted))
            g.FillEllipse(b, x, track.Top + (track.Height - knob) / 2, knob, knob);
        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(t.Text) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}

/// <summary>Accent-tinted rounded square holding a glyph, used as the app mark.</summary>
internal sealed class IconBadge : Control
{
    public IconBadge(string glyph)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        Text = glyph; Size = new(42, 42); Margin = new(0, 0, 12, 0);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var t = Theme.Current;
        e.Graphics.Clear(Parent?.BackColor ?? t.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Theme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), LogicalToDeviceUnits(10)))
        using (var b = new SolidBrush(t.Accent)) e.Graphics.FillPath(b, path);
        using var font = new Font(Theme.IconFamily, Height * 0.4f, GraphicsUnit.Pixel);
        TextRenderer.DrawText(e.Graphics, Text, font, ClientRectangle, t.AccentText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

/// <summary>Drop-down list with roomier, theme-colored items.</summary>
internal sealed class FlatCombo : ComboBox
{
    public FlatCombo()
    {
        DropDownStyle = ComboBoxStyle.DropDownList; DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 24; Dock = DockStyle.Fill; Margin = new(0, 2, 0, 2);
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.ApplyNative(this); }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var t = Theme.Current;
        bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
        bool selected = !edit && (e.State & DrawItemState.Selected) != 0;
        var back = edit ? BackColor : selected ? Theme.Blend(t.Surface, t.Accent, t.Dark ? 0.35f : 0.15f) : t.Surface;
        using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
        if (e.Index < 0) return;
        var bounds = Rectangle.Inflate(e.Bounds, -6, 0);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, bounds, t.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}

/// <summary>Activity log: muted time stamp followed by the message.</summary>
internal sealed class ActivityList : ListBox
{
    public ActivityList()
    {
        DrawMode = DrawMode.OwnerDrawFixed; BorderStyle = BorderStyle.None; IntegralHeight = false;
        ItemHeight = 24; Dock = DockStyle.Fill; SelectionMode = SelectionMode.None;
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.ApplyNative(this); }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var t = Theme.Current;
        using (var b = new SolidBrush(t.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
        if (e.Index < 0 || Items[e.Index] is not string line) return;
        var split = line.IndexOf("  ", StringComparison.Ordinal);
        var time = split > 0 ? line[..split] : "";
        var message = split > 0 ? line[(split + 2)..] : line;
        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        var timeWidth = TextRenderer.MeasureText(e.Graphics, "00:00:00", Font, Size.Empty, flags).Width + LogicalToDeviceUnits(12);
        TextRenderer.DrawText(e.Graphics, time, Font, e.Bounds with { Width = timeWidth }, t.Muted, flags);
        TextRenderer.DrawText(e.Graphics, message, Font,
            e.Bounds with { X = e.Bounds.X + timeWidth, Width = e.Bounds.Width - timeWidth }, t.Text, flags | TextFormatFlags.EndEllipsis);
    }
}
