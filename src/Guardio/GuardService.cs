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
    private readonly Dictionary<string, bool> controllerOverrides = new(StringComparer.Ordinal);
    private bool disposed;
    private readonly System.Windows.Forms.Timer recoveryTimer = new() { Interval = 1000 };
    public List<Endpoint> Outputs { get; private set; } = new();
    public bool Enabled => policy.Enabled;
    public string? FallbackId { get => policy.FallbackId; set => policy.FallbackId = value; }
    public IReadOnlyDictionary<string, bool> ControllerOverrides => controllerOverrides;
    public int Corrections { get; private set; }
    public List<string> Log { get; } = new();
    public event Action? Changed;
    public static JsonSerializerOptions JsonOptions { get; } = new()
    { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public GuardService(Control dispatcher, NativeAudio audio, string dataDir,
        IReadOnlyDictionary<string, bool>? controllerOverrides = null)
    {
        this.dispatcher = dispatcher; this.audio = audio; this.dataDir = dataDir;
        Directory.CreateDirectory(dataDir);
        foreach (var pair in controllerOverrides ?? new Dictionary<string, bool>())
            this.controllerOverrides[pair.Key] = pair.Value;
        ResetBaseline();
        audio.DevicesChanged += OnDevices;
        audio.DefaultChanged += OnDefault;
        audio.StartNotifications();
        // Also recover after a missed notification / sleep, without enforcing a fixed default.
        recoveryTimer.Tick += (_, _) => Safely(() => RefreshDevices());
        recoveryTimer.Start();
        AddLog("Prêt.");
    }

    public void Enable(bool enabled)
    {
        policy.SetEnabled(enabled);
        // Resuming must not be mistaken for a new connection.
        ResetBaseline();
        AddLog(enabled ? "Protection activée." : "Protection en pause.");
        Changed?.Invoke();
        WriteStatus();
    }

    /// <summary>Marks an output as a controller (or not), overriding name detection.</summary>
    public void SetController(string id, bool isController)
    {
        var detected = audio.Endpoints(all: true).FirstOrDefault(e => e.Id == id)?.IsController;
        if (detected == isController) controllerOverrides.Remove(id);
        else controllerOverrides[id] = isController;
        // A newly marked controller that is already connected is not a new connection.
        ResetBaseline();
        AddLog($"{Name(id)} : {(isController ? "traitée comme manette" : "n’est plus traitée comme manette")}.");
        Changed?.Invoke(); WriteStatus();
    }

    public void SelectManually(string id)
    {
        policy.AllowManualChange();
        audio.SetDefault(id, AudioRole.Console);
        audio.SetDefault(id, AudioRole.Multimedia);
        foreach (var pair in audio.Defaults()) policy.ObserveDefault(pair.Key, pair.Value, DateTimeOffset.UtcNow);
        AddLog($"Sortie choisie : {Name(id)}.");
        Changed?.Invoke(); WriteStatus();
    }

    private List<Endpoint> ReadOutputs() => audio.Endpoints(all: true)
        .Select(e => controllerOverrides.TryGetValue(e.Id, out var c) ? e with { IsController = c } : e).ToList();
    private void ResetBaseline()
    {
        Outputs = ReadOutputs();
        policy.Initialize(Outputs.Select(e => e.ToOutput()), audio.Defaults());
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
        Outputs = ReadOutputs();
        foreach (var id in policy.ObserveDevices(Outputs.Select(e => e.ToOutput()), DateTimeOffset.UtcNow))
            AddLog($"Manette branchée : {Name(id)}.");
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
                AddLog($"Sortie rétablie : {Name(decision.TargetId)} ({role}).");
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

    public string? CurrentId => audio.Default(AudioRole.Multimedia);
    public string CurrentName => Name(CurrentId);
    internal void SimulateArrivalForDiagnostics()
    {
        // Only the policy's availability snapshot changes. No device is disabled,
        // unplugged or restarted: this is not proof of a physical USB reconnection.
        var now = DateTimeOffset.UtcNow;
        policy.ObserveDevices(Outputs.Select(e => e.ToOutput() with { Active = !e.IsController && e.Active }), now);
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
            var status = new { version = Program.Version, at = DateTimeOffset.Now, enabled = Enabled,
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
