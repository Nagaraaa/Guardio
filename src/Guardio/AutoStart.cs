using Microsoft.Win32;

namespace Guardio;

/// <summary>Per-user Windows "Run" entry: no installer or admin rights needed.</summary>
internal static class AutoStart
{
    public const string TrayArgument = "--tray";
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Guardio";
    private static string Command => $"\"{Environment.ProcessPath}\" {TrayArgument}";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            if (value) key.SetValue(ValueName, Command);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>Points an existing entry at this copy, e.g. after the folder was moved.</summary>
    public static void Refresh()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        if (key?.GetValue(ValueName) is string current && current != Command) key.SetValue(ValueName, Command);
    }
}
