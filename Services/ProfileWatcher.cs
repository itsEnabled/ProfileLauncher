using ProfileLauncher.Models;

namespace ProfileLauncher.Services;

/// <summary>Raises Changed (on a background thread) when either browser rewrites its Local State file.</summary>
public sealed class ProfileWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();

    public event Action? Changed;

    public ProfileWatcher()
    {
        foreach (BrowserKind browser in Enum.GetValues<BrowserKind>())
        {
            string dir = ProfileDiscovery.UserDataDir(browser);
            if (!Directory.Exists(dir)) continue;

            try
            {
                // Chromium writes a temp file and renames it over "Local State", so watch renames too.
                var w = new FileSystemWatcher(dir, "Local State")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                w.Changed += (_, _) => Changed?.Invoke();
                w.Created += (_, _) => Changed?.Invoke();
                w.Renamed += (_, _) => Changed?.Invoke();
                w.EnableRaisingEvents = true;
                _watchers.Add(w);
            }
            catch
            {
                // No watcher for this browser; the re-scan on window activation still covers it.
            }
        }
    }

    public void Dispose()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
    }
}
