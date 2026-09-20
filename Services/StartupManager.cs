using Microsoft.Win32;

namespace ProfileLauncher.Services;

/// <summary>
/// "Start with Windows" for a portable exe: a per-user Run entry, no admin rights, no installer.
/// The registry is the single source of truth, so the checkbox always reflects what Windows will do.
/// </summary>
public static class StartupManager
{
    public const string TrayArgument = "--tray";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ProfileLauncher";

    private static string? Command =>
        Environment.ProcessPath is { } exe ? $"\"{exe}\" {TrayArgument}" : null;

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled && Command is { } command) key.SetValue(ValueName, command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// A portable exe gets moved around. If startup is on but points somewhere else, re-point it at
    /// the copy that is running now, so the entry can never go stale.
    /// </summary>
    public static void RefreshPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is string current && Command is { } command &&
                !string.Equals(current, command, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, command);
        }
        catch
        {
            // not worth interrupting startup for
        }
    }
}
