using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using GardeAudio.Core;

namespace GardeAudio;

internal static class Diagnostics
{
    private sealed record Snapshot(DateTimeOffset At, List<Endpoint> Outputs, List<Endpoint> Inputs,
        Dictionary<AudioRole, string?> Defaults, Dictionary<AudioRole, string?> InputDefaults,
        List<JoystickState> Joysticks);
    private sealed record CheckResult(string Name, bool Passed, string Detail);
    private sealed record JoystickState(uint Index, uint Result, uint X, uint Y, uint Buttons);

    public static int Run(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Commande : --inventory fichier.json | --live-test dossier | --set-output ID [rôle]");
        if (args[0] == "--live-test") return LiveTests(Path.GetFullPath(args[1]));
        using var audio = new NativeAudio();
        if (args[0] == "--inventory") { WriteJson(args[1], Capture(audio)); return 0; }
        if (args[0] == "--set-output")
        {
            var roles = args.Length > 2 ? new[] { Enum.Parse<AudioRole>(args[2], true) }
                : new[] { AudioRole.Console, AudioRole.Multimedia };
            foreach (var role in roles) audio.SetDefault(args[1], role);
            return 0;
        }
        throw new ArgumentException("Commande inconnue.");
    }

    private static Snapshot Capture(NativeAudio audio) => new(DateTimeOffset.Now,
        audio.Endpoints(all: true), audio.Endpoints(input: true, all: true),
        audio.Defaults(), audio.Defaults(input: true), ReadJoysticks());

    private static int LiveTests(string directory)
    {
        Directory.CreateDirectory(directory);
        using var dispatcher = new Control(); dispatcher.CreateControl();
        using var audio = new NativeAudio();
        var before = Capture(audio); WriteJson(Path.Combine(directory, "avant.json"), before);
        var checks = new List<CheckResult>();
        var headset = before.Outputs.FirstOrDefault(e => e.Active && e.Name.Contains("Logitech PRO X", StringComparison.OrdinalIgnoreCase));
        var dual = before.Outputs.FirstOrDefault(e => e.Active && e.IsDualSense);
        var other = before.Outputs.FirstOrDefault(e => e.Active && !e.IsDualSense && e.Id != headset?.Id);
        if (headset is null || dual is null)
        {
            checks.Add(new("Matériel nécessaire", false, "Brancher le casque Logitech PRO X et la DualSense USB."));
            WriteJson(Path.Combine(directory, "resultats.json"), checks); return 2;
        }
        using var guard = new GuardService(dispatcher, audio, Path.Combine(directory, "journal")) { FallbackId = headset.Id };
        try
        {
            guard.SelectManually(headset.Id); Pump(200);
            checks.Add(new("Sélection réelle du casque", MediaIs(audio, headset.Id), "Console et Multimedia."));
            var corrections = guard.Corrections;
            guard.SimulateArrivalForDiagnostics();
            var elapsed = Stopwatch.StartNew();
            audio.SetDefault(dual.Id, AudioRole.Multimedia);
            var returned = PumpUntil(() => audio.Default(AudioRole.Multimedia) == headset.Id && guard.Corrections > corrections, 2000);
            checks.Add(new("Rétablissement réel après arrivée simulée", returned,
                $"{elapsed.ElapsedMilliseconds} ms ; changement du périphérique Windows réel, cycle USB simulé uniquement dans la logique."));
            Pump(200); // Drain notifications for coupled or unchanged roles.
            // An allowed manual selection must survive an active arrival window.
            if (other is not null)
            {
                corrections = guard.Corrections;
                audio.SetDefault(other.Id, AudioRole.Console); audio.SetDefault(other.Id, AudioRole.Multimedia); Pump(350);
                checks.Add(new("Choix Windows manuel d’une autre sortie respecté", MediaIs(audio, other.Id) && guard.Corrections == corrections,
                    other.Name + $" ; corrections supplémentaires : {guard.Corrections - corrections}"));
            }
            guard.SelectManually(headset.Id); Pump(150);
            corrections = guard.Corrections;
            guard.SimulateArrivalForDiagnostics();
            guard.SelectManually(dual.Id); Pump(350);
            checks.Add(new("Choix explicite de la manette dans l’application respecté", MediaIs(audio, dual.Id) && guard.Corrections == corrections, "Annule la protection ponctuelle en cours."));
            guard.SelectManually(headset.Id); Pump(150);
            guard.SimulateArrivalForDiagnostics(); Pump(2150);
            corrections = guard.Corrections;
            audio.SetDefault(dual.Id, AudioRole.Console); audio.SetDefault(dual.Id, AudioRole.Multimedia); Pump(350);
            checks.Add(new("Choix Windows manuel de la manette hors branchement respecté", MediaIs(audio, dual.Id) && guard.Corrections == corrections, "Après expiration de la fenêtre de 2 s."));
            guard.SelectManually(headset.Id); Pump(150);
            guard.Enable(false); guard.SimulateArrivalForDiagnostics();
            audio.SetDefault(dual.Id, AudioRole.Multimedia); Pump(250);
            checks.Add(new("Pause effective", audio.Default(AudioRole.Multimedia) == dual.Id, "Aucune correction en pause."));
            guard.SelectManually(headset.Id); Pump(100);
            guard.Enable(true); Pump(100);
            var during = Capture(audio);
            checks.Add(new("Sorties et entrées restent actives", SameEndpointStates(before, during), "Aucun endpoint désactivé ou recréé."));
            checks.Add(new("Microphones inchangés", SameDefaults(before.InputDefaults, during.InputDefaults), "Les trois rôles d’entrée conservés."));
            var liveJoysticks = during.Joysticks.Where(j => j.Result == 0).Select(j => j.Index).ToArray();
            var beforeJoysticks = before.Joysticks.Where(j => j.Result == 0).Select(j => j.Index).ToArray();
            checks.Add(new("Lecture des contrôleurs encore disponible", beforeJoysticks.Length > 0 && beforeJoysticks.SequenceEqual(liveJoysticks),
                $"{liveJoysticks.Length} contrôleur(s) lisible(s) via WinMM ; ne prouve pas les vibrations ou les gâchettes."));
        }
        catch (Exception ex) { checks.Add(new("Exception pendant les essais", false, ex.ToString())); }
        finally
        {
            guard.Enable(false);
            var rollbackErrors = new List<string>();
            foreach (var pair in before.Defaults)
            {
                try { if (pair.Value is not null) audio.SetDefault(pair.Value, pair.Key); }
                catch (Exception ex) { rollbackErrors.Add(pair.Key + ": " + ex.Message); }
            }
            Pump(250);
            var after = Capture(audio); WriteJson(Path.Combine(directory, "apres.json"), after);
            checks.Add(new("Sorties initiales rétablies", rollbackErrors.Count == 0 && SameDefaults(before.Defaults, after.Defaults), string.Join("; ", rollbackErrors)));
            checks.Add(new("Entrées initiales conservées après les essais", SameDefaults(before.InputDefaults, after.InputDefaults), "Aucune modification de microphone."));
            WriteJson(Path.Combine(directory, "resultats.json"), new { at = DateTimeOffset.Now, checks,
                limitations = new[] { "Branchement USB physique non effectué par ce test.", "Rendu sonore, boutons physiques, vibrations et gâchettes à vérifier avec l’utilisateur." },
                log = guard.Log });
        }
        return checks.All(c => c.Passed) ? 0 : 1;
    }

    private static bool MediaIs(NativeAudio audio, string id) => audio.Default(AudioRole.Console) == id && audio.Default(AudioRole.Multimedia) == id;
    private static bool SameDefaults(Dictionary<AudioRole, string?> a, Dictionary<AudioRole, string?> b) => a.All(p => b.GetValueOrDefault(p.Key) == p.Value);
    private static bool SameEndpointStates(Snapshot a, Snapshot b) => a.Outputs.Concat(a.Inputs).All(e =>
        b.Outputs.Concat(b.Inputs).Any(n => n.Id == e.Id && n.State == e.State));
    private static void Pump(int milliseconds) => PumpUntil(() => false, milliseconds);
    private static bool PumpUntil(Func<bool> condition, int milliseconds)
    {
        var clock = Stopwatch.StartNew();
        do { Application.DoEvents(); if (condition()) return true; Thread.Sleep(10); } while (clock.ElapsedMilliseconds < milliseconds);
        return condition();
    }
    private static void WriteJson(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, GuardService.JsonOptions));
    }

    private static List<JoystickState> ReadJoysticks()
    {
        var results = new List<JoystickState>();
        for (uint i = 0; i < Math.Min(joyGetNumDevs(), 16); i++)
        {
            var info = new JoyInfo { Size = (uint)Marshal.SizeOf<JoyInfo>(), Flags = 0xff };
            var result = joyGetPosEx(i, ref info);
            if (result == 0) results.Add(new(i, result, info.X, info.Y, info.Buttons));
        }
        return results;
    }
    [StructLayout(LayoutKind.Sequential)] private struct JoyInfo
    { public uint Size, Flags, X, Y, Z, R, U, V, Buttons, ButtonNumber, Pov, Reserved1, Reserved2; }
    [DllImport("winmm.dll")] private static extern uint joyGetNumDevs();
    [DllImport("winmm.dll")] private static extern uint joyGetPosEx(uint id, ref JoyInfo info);
}
