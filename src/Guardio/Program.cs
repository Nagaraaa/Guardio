namespace Guardio;

internal static class Program
{
    public static string Version { get; } = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // Windows startup launches Guardio straight into the notification area.
        bool tray = args is [AutoStart.TrayArgument];
        bool diagnostics = args.Length > 0 && !tray;
        try
        {
            if (diagnostics) return Diagnostics.Run(args);
            // Same key as GardeAudio, so an older copy can't fight Guardio over the audio defaults.
            using var mutex = new Mutex(true, "Local\\GardeAudio.DualSense.v01", out bool first);
            if (!first)
            {
                if (!tray) MessageBox.Show("Guardio est déjà ouvert dans la zone de notification.", "Guardio");
                return 0;
            }
            Application.Run(new MainForm(Path.Combine(AppContext.BaseDirectory, "donnees"), startHidden: tray));
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "erreur-demarrage.txt"), ex.ToString());
            if (!diagnostics) MessageBox.Show(ex.Message, "Guardio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
