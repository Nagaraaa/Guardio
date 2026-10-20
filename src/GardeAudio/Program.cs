using System.Text.Json;

namespace GardeAudio;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Length > 0) return Diagnostics.Run(args);
            using var mutex = new Mutex(true, "Local\\GardeAudio.DualSense.v01", out bool first);
            if (!first) { MessageBox.Show("GardeAudio est déjà ouvert près de l’horloge.", "GardeAudio"); return 0; }
            Application.Run(new MainForm(Path.Combine(AppContext.BaseDirectory, "donnees")));
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "erreur-demarrage.txt"), ex.ToString());
            if (args.Length == 0) MessageBox.Show(ex.Message, "GardeAudio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
