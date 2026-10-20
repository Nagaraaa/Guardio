using System.Text.Json;

namespace Guardio;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Length > 0) return Diagnostics.Run(args);
            // Retain the original instance key so an older GardeAudio cannot run
            // alongside Guardio and compete for the same Windows audio defaults.
            using var mutex = new Mutex(true, "Local\\GardeAudio.DualSense.v01", out bool first);
            if (!first) { MessageBox.Show("La protection audio est déjà ouverte près de l’horloge.", "Guardio"); return 0; }
            Application.Run(new MainForm(Path.Combine(AppContext.BaseDirectory, "donnees")));
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "erreur-demarrage.txt"), ex.ToString());
            if (args.Length == 0) MessageBox.Show(ex.Message, "Guardio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
