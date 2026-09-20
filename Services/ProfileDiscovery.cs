using System.Text.Json;
using System.Windows.Media;
using ProfileLauncher.Models;

namespace ProfileLauncher.Services;

/// <summary>Reads profile.info_cache out of each browser's Local State file.</summary>
public static class ProfileDiscovery
{
    public static string UserDataDir(BrowserKind browser) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        browser == BrowserKind.Chrome ? @"Google\Chrome\User Data" : @"Microsoft\Edge\User Data");

    public static string LocalStatePath(BrowserKind browser) => Path.Combine(UserDataDir(browser), "Local State");

    public static List<BrowserProfile> Discover()
    {
        var found = new List<BrowserProfile>();
        foreach (BrowserKind browser in Enum.GetValues<BrowserKind>())
        {
            try
            {
                found.AddRange(ReadBrowser(browser));
            }
            catch
            {
                // Browser not installed, or the file was caught mid-write. The watcher will bring us back.
            }
        }
        return found
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Browser)
            .ToList();
    }

    private static List<BrowserProfile> ReadBrowser(BrowserKind browser)
    {
        var list = new List<BrowserProfile>();
        string path = LocalStatePath(browser);
        if (!File.Exists(path)) return list;

        // The browser keeps this file open and replaces it often, so share everything.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("profile", out var profile) ||
            !profile.TryGetProperty("info_cache", out var cache) ||
            cache.ValueKind != JsonValueKind.Object)
            return list;

        foreach (var entry in cache.EnumerateObject())
        {
            var info = entry.Value;
            if (info.ValueKind != JsonValueKind.Object) continue;
            if (info.TryGetProperty("is_ephemeral", out var eph) && eph.ValueKind == JsonValueKind.True) continue;

            string name = info.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() ?? ""
                : "";
            if (string.IsNullOrWhiteSpace(name)) name = entry.Name;
            name = name.Trim();

            list.Add(new BrowserProfile(browser, entry.Name, name, ReadColor(info) ?? ColorFromName(name)));
        }
        return list;
    }

    /// <summary>Profile theme colour. Chromium stores these as signed 32-bit ARGB integers.</summary>
    private static Color? ReadColor(JsonElement info)
    {
        foreach (string key in new[] { "profile_highlight_color", "default_avatar_fill_color" })
        {
            if (info.TryGetProperty(key, out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt64(out long raw))
            {
                uint argb = unchecked((uint)raw);
                return Color.FromRgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
            }
        }
        return null;
    }

    /// <summary>Fallback when the browser recorded no colour: a stable hue derived from the name.</summary>
    private static Color ColorFromName(string name)
    {
        uint hash = 2166136261;                       // FNV-1a; string.GetHashCode changes every run
        foreach (char ch in name.ToLowerInvariant()) hash = unchecked((hash ^ ch) * 16777619);
        return FromHsl(hash % 360, 0.55, 0.45);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = l - c / 2;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}
