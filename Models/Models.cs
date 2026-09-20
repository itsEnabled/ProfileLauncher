using System.Text.Json.Serialization;
using System.Windows.Input;
using System.Windows.Media;

namespace ProfileLauncher.Models;

public enum BrowserKind { Chrome, Edge }

public enum TenantEnvironment { Unset, Gcch, Commercial }

public enum ThemeChoice { FollowSystem, Light, Dark }

/// <summary>One browser profile as found in a browser's Local State file.</summary>
public sealed record BrowserProfile(BrowserKind Browser, string Folder, string Name, Color Color)
{
    /// <summary>Stable id used in state.json, e.g. "Chrome|Profile 12".</summary>
    public string Key => $"{Browser}|{Folder}";
}

/// <summary>One row of portals.json: the same portal in both clouds.</summary>
public sealed class Portal
{
    public string Name { get; set; } = "";
    public string Commercial { get; set; } = "";
    public string Gcch { get; set; } = "";

    public string UrlFor(TenantEnvironment env) => env == TenantEnvironment.Gcch ? Gcch : Commercial;
}

/// <summary>A global hotkey: Win32 modifier flags plus one virtual-key code.</summary>
public sealed class HotkeySetting
{
    public const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModWin = 0x8;

    public uint Modifiers { get; set; } = ModControl | ModAlt;
    public int VirtualKey { get; set; } = 0x20;                    // Space

    [JsonIgnore]
    public string Display
    {
        get
        {
            var parts = new List<string>();
            if ((Modifiers & ModControl) != 0) parts.Add("Ctrl");
            if ((Modifiers & ModAlt) != 0) parts.Add("Alt");
            if ((Modifiers & ModShift) != 0) parts.Add("Shift");
            if ((Modifiers & ModWin) != 0) parts.Add("Win");

            string key = KeyInterop.KeyFromVirtualKey(VirtualKey).ToString();
            if (key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1])) key = key[1..];   // D1 -> 1
            parts.Add(key);
            return string.Join(" + ", parts);
        }
    }
}

/// <summary>What the launcher has learned about one client.</summary>
public sealed class ClientState
{
    public TenantEnvironment Environment { get; set; } = TenantEnvironment.Unset;
    public bool Favourite { get; set; }
    public DateTimeOffset? LastOpened { get; set; }
}

/// <summary>Everything persisted in state.json.</summary>
public sealed class AppState
{
    public double TileSize { get; set; } = 88;
    public ThemeChoice Theme { get; set; } = ThemeChoice.FollowSystem;
    public HotkeySetting Hotkey { get; set; } = new();
    public bool CloseToTray { get; set; } = true;
    public bool HideAfterLaunch { get; set; }
    public bool TrayHintShown { get; set; }
    public Dictionary<string, ClientState> Clients { get; set; } = new();
}
