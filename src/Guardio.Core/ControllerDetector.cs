namespace Guardio.Core;

/// <summary>
/// Recognizes the audio outputs that game controllers expose to Windows.
/// Anything not covered here can be marked manually in the application.
/// </summary>
public static class ControllerDetector
{
    // Sony: "DualSense Wireless Controller", "DualSense Edge Wireless Controller",
    // "Wireless Controller" (DualShock 4). Microsoft: "Xbox Controller",
    // "Xbox Wireless Controller". Third-party pads often use "Gamepad".
    private static readonly string[] Keywords =
        ["DualSense", "DualShock", "Wireless Controller", "Xbox Controller", "Gamepad"];

    public static bool Matches(params string?[] names) => names.Any(name => name is not null
        && Keywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)));
}
