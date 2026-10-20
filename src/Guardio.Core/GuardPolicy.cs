namespace Guardio.Core;

public enum AudioRole { Console, Multimedia, Communications }

public record OutputDevice(string Id, string Name, bool Active, bool IsDualSense);
public record RestoreDecision(AudioRole Role, string TargetId, string RejectedId);

/// <summary>
/// A short, single-use protection for each output role after a DualSense
/// becomes active. Never pins an output indefinitely or modifies input devices.
/// All calls are serialized by the application on its UI thread.
/// </summary>
public sealed class GuardPolicy
{
    public static readonly TimeSpan ArrivalWindow = TimeSpan.FromSeconds(2);
    private Dictionary<string, OutputDevice> devices = new(StringComparer.Ordinal);
    private readonly Dictionary<AudioRole, string?> current = new();
    private readonly Dictionary<AudioRole, string?> previousAllowed = new();
    private readonly Dictionary<string, Arrival> arrivals = new(StringComparer.Ordinal);
    private bool initialized;
    public bool Enabled { get; private set; } = true;
    public string? FallbackId { get; set; }
    public int RestoreCount { get; private set; }

    private sealed record Arrival(DateTimeOffset Until, Dictionary<AudioRole, string?> Before,
        HashSet<AudioRole> Consumed);

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        arrivals.Clear();
    }

    public void Initialize(IEnumerable<OutputDevice> outputs, IReadOnlyDictionary<AudioRole, string?> defaults)
    {
        devices = outputs.ToDictionary(d => d.Id, StringComparer.Ordinal);
        current.Clear(); previousAllowed.Clear(); arrivals.Clear();
        foreach (var role in Enum.GetValues<AudioRole>())
        {
            defaults.TryGetValue(role, out var id);
            current[role] = id;
            if (IsAllowedActive(id)) previousAllowed[role] = id;
        }
        initialized = true;
    }

    public IReadOnlyList<string> ObserveDevices(IEnumerable<OutputDevice> outputs, DateTimeOffset now)
    {
        var next = outputs.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var armed = new List<string>();
        if (initialized && Enabled)
        {
            foreach (var d in next.Values.Where(d => d.Active && d.IsDualSense))
            {
                if (!devices.TryGetValue(d.Id, out var old) || !old.Active)
                {
                    // Snapshot the actual prior selections, separately for every role.
                    arrivals[d.Id] = new Arrival(now + ArrivalWindow,
                        new Dictionary<AudioRole, string?>(current), new());
                    armed.Add(d.Id);
                }
            }
        }
        devices = next;
        foreach (var id in arrivals.Keys.ToArray())
            if (!devices.TryGetValue(id, out var d) || !d.Active || now > arrivals[id].Until)
                arrivals.Remove(id);
        return armed;
    }

    public RestoreDecision? ObserveDefault(AudioRole role, string? id, DateTimeOffset now)
    {
        var priorId = current.GetValueOrDefault(role);
        current[role] = id;
        if (IsAllowedActive(id))
        {
            previousAllowed[role] = id;
            // Accept a user-selected non-controller output, including during arrival.
            foreach (var a in arrivals.Values) a.Before[role] = id;
            return null;
        }
        // Windows may notify unchanged roles when another role is selected.
        // An existing controller selection is not a new automatic switch.
        if (id == priorId) return null;
        if (!Enabled || id is null || !devices.TryGetValue(id, out var output)
            || !output.Active || !output.IsDualSense || !arrivals.TryGetValue(id, out var arrival)
            || now > arrival.Until || arrival.Consumed.Contains(role)) return null;

        arrival.Before.TryGetValue(role, out var before);
        previousAllowed.TryGetValue(role, out var previous);
        var target = new[] { before, previous, FallbackId }.FirstOrDefault(IsAllowedActive);
        // Consume once, even if no fallback exists: never fight a subsequent manual choice.
        arrival.Consumed.Add(role);
        return target is null ? null : new RestoreDecision(role, target, id);
    }

    public void AcknowledgeRestore(RestoreDecision decision, bool success)
    {
        if (!success) return;
        if (arrivals.TryGetValue(decision.RejectedId, out var arrival))
            arrival.Consumed.Add(decision.Role);
        current[decision.Role] = decision.TargetId;
        previousAllowed[decision.Role] = decision.TargetId;
        RestoreCount++;
    }

    public void AllowManualChange()
    {
        // Explicit action in our window/tray is always stronger than a pending arrival.
        arrivals.Clear();
    }

    private bool IsAllowedActive(string? id) => id is not null
        && devices.TryGetValue(id, out var d) && d.Active && !d.IsDualSense;
}
