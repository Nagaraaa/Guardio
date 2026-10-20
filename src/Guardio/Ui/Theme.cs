using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Guardio.Ui;

internal sealed record Palette(bool Dark, Color Background, Color Surface, Color Subtle, Color Border,
    Color Text, Color Muted, Color Accent, Color AccentText, Color Success, Color Warning);

/// <summary>Light and dark palettes that follow the Windows app theme.</summary>
internal static class Theme
{
    private static readonly Palette Light = new(false,
        Background: Color.FromArgb(243, 244, 247), Surface: Color.White, Subtle: Color.FromArgb(236, 238, 242),
        Border: Color.FromArgb(225, 228, 233), Text: Color.FromArgb(27, 29, 33), Muted: Color.FromArgb(105, 111, 121),
        Accent: Color.FromArgb(62, 99, 221), AccentText: Color.White,
        Success: Color.FromArgb(22, 150, 85), Warning: Color.FromArgb(205, 120, 10));
    private static readonly Palette DarkPalette = new(true,
        Background: Color.FromArgb(24, 25, 28), Surface: Color.FromArgb(34, 36, 40), Subtle: Color.FromArgb(46, 48, 54),
        Border: Color.FromArgb(50, 53, 59), Text: Color.FromArgb(236, 237, 240), Muted: Color.FromArgb(152, 157, 166),
        Accent: Color.FromArgb(118, 146, 255), AccentText: Color.FromArgb(16, 18, 26),
        Success: Color.FromArgb(70, 210, 140), Warning: Color.FromArgb(245, 170, 60));

    public static Palette Current { get; private set; } = Detect();
    public static event Action? Changed;

    static Theme() => SystemEvents.UserPreferenceChanged += (_, e) =>
    {
        if (e.Category != UserPreferenceCategory.General) return;
        var next = Detect();
        if (next == Current) return;
        Current = next; Changed?.Invoke();
    };

    private static Palette Detect()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is 0 ? DarkPalette : Light;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException) { return Light; }
    }

    public static string IconFamily { get; } = new InstalledFontCollection().Families
        .Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    public static Font IconFont(float size) => new(IconFamily, size);
    public const string Headphones = "", Gamepad = "", Shield = "", Speaker = "",
        History = "", Settings = "";

    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Color Blend(Color a, Color b, float amount) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * amount), (int)(a.G + (b.G - a.G) * amount), (int)(a.B + (b.B - a.B) * amount));

    /// <summary>Dark title bar and native dark scrollbars / drop-downs on Windows 10 1809+.</summary>
    public static void ApplyWindow(Form form)
    {
        if (!form.IsHandleCreated) return;
        int dark = Current.Dark ? 1 : 0;
        DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
    }
    /// <summary>Recolors a window and its standard controls after a theme change.</summary>
    public static void Apply(Form form)
    {
        form.BackColor = Current.Background; form.ForeColor = Current.Text;
        ApplyWindow(form);
        Recolor(form);
        form.Invalidate(true);
    }
    private static void Recolor(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case Card: c.BackColor = Current.Surface; ApplyNative(c); break;
                case MutedLabel: c.ForeColor = Current.Muted; break;
                case Glyph: c.ForeColor = Current.Accent; break;
                case ComboBox or ListBox: c.BackColor = Current.Surface; c.ForeColor = Current.Text; ApplyNative(c); break;
            }
            Recolor(c);
        }
    }

    public static void ApplyNative(Control control)
    {
        if (control.IsHandleCreated)
            SetWindowTheme(control.Handle, Current.Dark ? (control is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer") : "Explorer", null);
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string? app, string? idList);
}
