using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using ProfileLauncher.Models;
using ProfileLauncher.Services;

namespace ProfileLauncher.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    /// <summary>The tile size the XAML is drawn at. Everything else is this, scaled.</summary>
    public const double BaseTileSize = 88;

    private readonly ConfigStore _store = new();
    private readonly AppState _state;
    private string _searchText = "";
    private string _browserFilter = "All";
    private bool _showFavouritesOnly;
    private bool _isSettingsOpen;

    public MainViewModel()
    {
        _state = _store.LoadState();
        _state.Hotkey ??= new HotkeySetting();
        _state.Clients ??= new Dictionary<string, ClientState>();
        Portals = _store.LoadPortals();

        View = CollectionViewSource.GetDefaultView(Tiles);
        View.Filter = Matches;

        Refresh();
    }

    public ObservableCollection<TileViewModel> Tiles { get; } = new();
    public ICollectionView View { get; }
    public List<Portal> Portals { get; }

    public string ConfigLocation => (_store.IsPortable ? "Portable  ·  " : "") + _store.Folder;

    // ---- filtering -------------------------------------------------------------------------

    public string SearchText
    {
        get => _searchText;
        set { if (Set(ref _searchText, value)) RefreshView(); }
    }

    public string BrowserFilter
    {
        get => _browserFilter;
        set { if (Set(ref _browserFilter, value)) RefreshView(); }
    }

    public bool ShowFavouritesOnly
    {
        get => _showFavouritesOnly;
        set { if (Set(ref _showFavouritesOnly, value)) RefreshView(); }
    }

    public string CountText => Tiles.Count == 1 ? "1 profile" : $"{Tiles.Count} profiles";

    public bool IsEmpty => !View.Cast<object>().Any();

    public string EmptyText =>
        Tiles.Count == 0 ? "No Chrome or Edge profiles found for this Windows account."
        : _showFavouritesOnly && _searchText.Trim().Length == 0 ? "No favorites yet. Hover over a tile and click the star."
        : "Nothing matches.";

    // ---- settings --------------------------------------------------------------------------

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => Set(ref _isSettingsOpen, value);
    }

    /// <summary>Slider value in pixels. Not saved on every tick; the window saves once the slider rests.</summary>
    public double TileSize
    {
        get => _state.TileSize;
        set
        {
            if (Math.Abs(_state.TileSize - value) < 0.01) return;
            _state.TileSize = value;
            Raise();
            Raise(nameof(Scale));
            Raise(nameof(TileSizeText));
        }
    }

    /// <summary>The one number the whole grid scales from, so resizing is a single property change.</summary>
    public double Scale => _state.TileSize / BaseTileSize;

    public string TileSizeText => $"{_state.TileSize:0} px";

    public ThemeChoice Theme
    {
        get => _state.Theme;
        set
        {
            if (_state.Theme == value) return;
            _state.Theme = value;
            Raise();
            Save();
        }
    }

    public HotkeySetting Hotkey
    {
        get => _state.Hotkey;
        set
        {
            _state.Hotkey = value;
            Raise();
            Raise(nameof(HotkeyText));
            Save();
        }
    }

    public string HotkeyText => _state.Hotkey.Display;

    public bool CloseToTray
    {
        get => _state.CloseToTray;
        set
        {
            if (_state.CloseToTray == value) return;
            _state.CloseToTray = value;
            Raise();
            Save();
        }
    }

    public bool HideAfterLaunch
    {
        get => _state.HideAfterLaunch;
        set
        {
            if (_state.HideAfterLaunch == value) return;
            _state.HideAfterLaunch = value;
            Raise();
            Save();
        }
    }

    /// <summary>Backed by the registry rather than state.json, so it always shows what Windows will really do.</summary>
    public bool StartWithWindows
    {
        get => StartupManager.IsEnabled();
        set
        {
            try { StartupManager.Set(value); } catch { /* registry locked down; the box snaps back */ }
            Raise();
        }
    }

    public bool TrayHintShown
    {
        get => _state.TrayHintShown;
        set { _state.TrayHintShown = value; Save(); }
    }

    public void Save() => _store.SaveState(_state);

    // ---- profiles --------------------------------------------------------------------------

    /// <summary>
    /// Re-reads both browsers. Rebuilds the grid only if something actually changed, because the
    /// browsers rewrite Local State constantly for reasons that have nothing to do with profiles.
    /// </summary>
    public void Refresh()
    {
        var found = ProfileDiscovery.Discover();

        bool same = found.Count == Tiles.Count &&
                    found.Zip(Tiles, (p, t) => p == t.Profile).All(equal => equal);
        if (same) return;

        Tiles.Clear();
        foreach (var profile in found)
            Tiles.Add(new TileViewModel(profile, StateFor(profile.Key)));

        Raise(nameof(CountText));
        RaiseEmpty();
    }

    public void SetEnvironment(TileViewModel tile, TenantEnvironment env)
    {
        tile.Environment = env;
        Save();
    }

    public void ToggleFavourite(TileViewModel tile)
    {
        tile.IsFavourite = !tile.IsFavourite;
        Save();
        if (_showFavouritesOnly) RefreshView();
    }

    /// <summary>Opens the portal using the tile's environment as it is right now, never a cached URL.</summary>
    public void Launch(TileViewModel tile, Portal portal)
    {
        string url = portal.UrlFor(tile.Environment);
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException(
                $"portals.json has no {TileViewModel.EnvironmentLabel(tile.Environment)} URL for {portal.Name}.");

        BrowserLauncher.Open(tile.Profile, url);

        tile.LastOpened = DateTimeOffset.Now;
        Save();
    }

    /// <summary>True if a tile with this name already exists in that browser.</summary>
    public bool Exists(BrowserKind browser, string clientName) =>
        Tiles.Any(t => t.Profile.Browser == browser &&
                       string.Equals(t.Name, clientName, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>
    /// Creates the profile folder, remembers its environment so the first click never asks, then
    /// opens it at the first portal. That first launch is what makes the browser register it.
    /// </summary>
    public void CreateProfile(BrowserKind browser, string clientName, System.Windows.Media.Color colour,
                              TenantEnvironment env)
    {
        var profile = ProfileCreator.Create(browser, clientName, colour);

        var client = StateFor(profile.Key);
        client.Environment = env;
        client.LastOpened = DateTimeOffset.Now;
        Save();

        var first = Portals.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.UrlFor(env)));
        BrowserLauncher.Open(profile, first?.UrlFor(env) ?? "about:blank");
    }

    private ClientState StateFor(string key)
    {
        if (!_state.Clients.TryGetValue(key, out var client))
            _state.Clients[key] = client = new ClientState();
        return client;
    }

    private void RefreshView()
    {
        View.Refresh();
        RaiseEmpty();
    }

    private void RaiseEmpty()
    {
        Raise(nameof(IsEmpty));
        Raise(nameof(EmptyText));
    }

    private bool Matches(object item)
    {
        var tile = (TileViewModel)item;
        if (_showFavouritesOnly && !tile.IsFavourite) return false;
        if (_browserFilter != "All" && tile.Profile.Browser.ToString() != _browserFilter) return false;

        string query = _searchText.Trim();
        return query.Length == 0 || tile.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }
}
