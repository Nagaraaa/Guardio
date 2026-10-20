using System.Text.Json;
using System.Text.Json.Serialization;
using Guardio.Core;

namespace Guardio;

public sealed class GuardService : IDisposable
{
    private readonly NativeAudio audio;
    private readonly Control dispatcher;
    private readonly string dataDir;
    private readonly GuardPolicy policy = new();
    private bool disposed;
    private readonly System.Windows.Forms.Timer recoveryTimer = new() { Interval = 1000 };
    public List<Endpoint> Outputs { get; private set; }
    public bool Enabled => policy.Enabled;
    public string? FallbackId { get => policy.FallbackId; set => policy.FallbackId = value; }
    public int Corrections { get; private set; }
    public List<string> Log { get; } = new();
    public event Action? Changed;
    public static JsonSerializerOptions JsonOptions { get; } = new()
    { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public GuardService(Control dispatcher, NativeAudio audio, string dataDir)
    {
        this.dispatcher = dispatcher; this.audio = audio; this.dataDir = dataDir;
        Directory.CreateDirectory(dataDir);
        Outputs = audio.Endpoints(all: true);
        policy.Initialize(Outputs.Select(e => e.ToOutput()), audio.Defaults());
        audio.DevicesChanged += OnDevices;
        audio.DefaultChanged += OnDefault;
        audio.StartNotifications();
        // Also recover after a missed notification / sleep, without enforcing a fixed default.
        recoveryTimer.Tick += (_, _) => Safely(() => RefreshDevices());
        recoveryTimer.Start();
        AddLog("Prêt. Les sorties déjà connectées ne sont pas verrouillées.");
    }

    public void Enable(bool enabled)
    {
        policy.SetEnabled(enabled);
        // Reset the baseline: resuming must not be mistaken for a new connection.
        Outputs = audio.Endpoints(all: true);
        policy.Initialize(Outputs.Select(e => e.ToOutput()), audio.Defaults());
        AddLog(enabled ? "Protection active au prochain branchement." : "Protection en pause.");
        Changed?.Invoke();
        WriteStatus();
    }

    public void SelectManually(string id)
    {
        policy.AllowManualChange();
        audio.SetDefault(id, AudioRole.Console);
        audio.SetDefault(id, AudioRole.Multimedia);
        foreach (var pair in audio.Defaults()) policy.ObserveDefault(pair.Key, pair.Value, DateTimeOffset.UtcNow);
        AddLog($"Choix manuel : {Name(id)}.");
        Changed?.Invoke(); WriteStatus();
    }

    private void OnDevices(string _) => Post(RefreshDevices);
    private void OnDefault(int flow, AudioRole role, string? id)
    {
        if (flow != 0) return; // Microphones are never modified.
        Post(() => HandleDefault(role, id));
    }
    private void Post(Action action)
    {
        if (disposed || dispatcher.IsDisposed || !dispatcher.IsHandleCreated) return;
        try { dispatcher.BeginInvoke(() => { if (!disposed) Safely(action); }); }
        catch (InvalidOperationException) { }
    }
    private void Safely(Action action)
    {
        try { action(); }
        catch (Exception ex) { AddLog("Erreur audio : " + ex.Message); Changed?.Invoke(); }
    }

    public void RefreshDevices()
    {
        Outputs = audio.Endpoints(all: true);
        foreach (var id in policy.ObserveDevices(Outputs.Select(e => e.ToOutput()), DateTimeOffset.UtcNow))
            AddLog($"Branchement détecté : {Name(id)}. Protection ponctuelle pendant 2 s.");
        Changed?.Invoke(); WriteStatus();
    }

    private void HandleDefault(AudioRole role, string? id)
    {
        RefreshDevices();
        // A newer choice may already have superseded this queued callback.
        if (audio.Default(role) != id) return;
        var decision = policy.ObserveDefault(role, id, DateTimeOffset.UtcNow);
        if (decision is null) { Changed?.Invoke(); WriteStatus(); return; }
        var before = audio.Defaults();
        try
        {
            audio.SetDefault(decision.TargetId, role);
            var after = audio.Defaults();
            bool success = after[role] == decision.TargetId;
            policy.AcknowledgeRestore(decision, success);
            // Windows can couple console and multimedia roles. Consume both if
            // this one setter changed both, so a later manual choice stays free.
            foreach (var r in Enum.GetValues<AudioRole>().Where(r => r != role))
                if (before[r] == decision.RejectedId && after[r] == decision.TargetId)
                    policy.AcknowledgeRestore(decision with { Role = r }, true);
            if (success)
            {
                Corrections++;
                AddLog($"Bascule au branchement corrigée ({role}) → {Name(decision.TargetId)}.");
            }
            else AddLog("Windows n’a pas confirmé le rétablissement de la sortie.");
        }
        catch (Exception ex)
        {
            policy.AcknowledgeRestore(decision, false);
            AddLog("Rétablissement impossible : " + ex.Message);
        }
        Changed?.Invoke(); WriteStatus();
    }

    public string CurrentName => Name(audio.Default(AudioRole.Multimedia));
    internal void SimulateArrivalForDiagnostics()
    {
        // Only the policy's availability snapshot changes. No device is disabled,
        // unplugged or restarted: this is not proof of a physical USB reconnection.
        var now = DateTimeOffset.UtcNow;
        policy.ObserveDevices(Outputs.Select(e => e.ToOutput() with { Active = e.IsDualSense ? false : e.Active }), now);
        policy.ObserveDevices(Outputs.Select(e => e.ToOutput()), now);
        AddLog("TEST : arrivée simulée dans la logique, périphériques laissés actifs.");
    }
    private string Name(string? id) => Outputs.FirstOrDefault(e => e.Id == id)?.Name ?? "Aucune sortie disponible";
    public void AddLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        Log.Add(line); if (Log.Count > 150) Log.RemoveAt(0);
        try { File.AppendAllText(Path.Combine(dataDir, "journal.txt"), line + Environment.NewLine); } catch (IOException) { }
    }

    public void WriteStatus()
    {
        try
        {
            var status = new { version = "0.1.1", at = DateTimeOffset.Now, enabled = Enabled,
                fallbackId = FallbackId, corrections = Corrections,
                outputs = Outputs, defaults = audio.Defaults(), inputs = audio.Defaults(input: true),
                recentEvents = Log.TakeLast(15).ToArray() };
            var path = Path.Combine(dataDir, "etat.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(status, JsonOptions));
            File.Move(path + ".tmp", path, true);
        }
        catch (IOException) { }
    }

    public void Dispose()
    {
        disposed = true; recoveryTimer.Stop(); recoveryTimer.Dispose();
        audio.DevicesChanged -= OnDevices; audio.DefaultChanged -= OnDefault;
        audio.Dispose();
    }
}
