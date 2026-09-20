using System.Text.Json;
using System.Text.Json.Serialization;
using ProfileLauncher.Models;

namespace ProfileLauncher.Services;

/// <summary>
/// One build, two behaviours: if a state.json sits next to the exe (even an empty one)
/// and that folder is writable, run portable. Otherwise use %APPDATA%\ProfileLauncher.
/// </summary>
public sealed class ConfigStore
{
    private const string StateFile = "state.json";
    private const string PortalsFile = "portals.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Folder { get; }
    public bool IsPortable { get; }

    public ConfigStore()
    {
        string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

        if (File.Exists(Path.Combine(exeDir, StateFile)) && IsWritable(exeDir))
        {
            Folder = exeDir;
            IsPortable = true;
        }
        else
        {
            Folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProfileLauncher");
            Directory.CreateDirectory(Folder);
        }
    }

    public AppState LoadState()
    {
        try
        {
            string path = Path.Combine(Folder, StateFile);
            if (!File.Exists(path)) return new AppState();
            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return new AppState();   // the empty portable marker
            return JsonSerializer.Deserialize<AppState>(text, Json) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    public void SaveState(AppState state)
    {
        try
        {
            string path = Path.Combine(Folder, StateFile);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(state, Json));
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // Losing a timestamp is not worth interrupting a launch for.
        }
    }

    /// <summary>Loads portals.json, writing the defaults the first time so they can be edited.</summary>
    public List<Portal> LoadPortals()
    {
        string path = Path.Combine(Folder, PortalsFile);
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<List<Portal>>(File.ReadAllText(path), Json);
                if (loaded is { Count: > 0 })
                {
                    if (ApplyCorrections(loaded))
                        File.WriteAllText(path, JsonSerializer.Serialize(loaded, Json));
                    return loaded;
                }
            }
            else
            {
                File.WriteAllText(path, JsonSerializer.Serialize(DefaultPortals(), Json));
            }
        }
        catch
        {
            // Bad or unreadable file: run on defaults and leave the file alone so it can be fixed.
        }
        return DefaultPortals();
    }

    /// <summary>
    /// Default URLs this launcher shipped with that turned out to be wrong, and what they should be.
    /// portals.json is written once and never overwritten, so without this an early mistake would
    /// live on in every copy that had already run. Only exact matches of the old default are touched;
    /// anything the user typed themselves is left alone.
    /// </summary>
    private static readonly Dictionary<string, string> Corrections = new(StringComparer.OrdinalIgnoreCase)
    {
        ["https://admin.teams.microsoft.us"] = "https://admin.gov.teams.microsoft.us"
    };

    private static bool ApplyCorrections(List<Portal> portals)
    {
        bool changed = false;
        foreach (var portal in portals)
        {
            if (Corrections.TryGetValue(portal.Gcch.Trim().TrimEnd('/'), out string? gcch))
            {
                portal.Gcch = gcch;
                changed = true;
            }
            if (Corrections.TryGetValue(portal.Commercial.Trim().TrimEnd('/'), out string? commercial))
            {
                portal.Commercial = commercial;
                changed = true;
            }
        }
        return changed;
    }

    private static List<Portal> DefaultPortals() => new()
    {
        new() { Name = "Entra",               Commercial = "https://entra.microsoft.com",          Gcch = "https://entra.microsoft.us" },
        new() { Name = "Microsoft 365 admin", Commercial = "https://admin.microsoft.com",          Gcch = "https://portal.office365.us/adminportal/home" },
        new() { Name = "Exchange",            Commercial = "https://admin.exchange.microsoft.com", Gcch = "https://admin.exchange.office365.us" },
        new() { Name = "Intune",              Commercial = "https://intune.microsoft.com",         Gcch = "https://intune.microsoft.us" },
        new() { Name = "Teams",               Commercial = "https://admin.teams.microsoft.com",    Gcch = "https://admin.gov.teams.microsoft.us" },
        new() { Name = "Defender",            Commercial = "https://security.microsoft.com",       Gcch = "https://security.microsoft.us" },
        new() { Name = "Purview",             Commercial = "https://purview.microsoft.com",        Gcch = "https://purview.microsoft.us" },
        new() { Name = "Azure portal",        Commercial = "https://portal.azure.com",             Gcch = "https://portal.azure.us" },
    };

    private static bool IsWritable(string dir)
    {
        try
        {
            string probe = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
