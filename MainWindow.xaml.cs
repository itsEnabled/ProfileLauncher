using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ProfileLauncher.Models;
using ProfileLauncher.Services;
using ProfileLauncher.ViewModels;

namespace ProfileLauncher;

public partial class MainWindow : Window
{
    private const string HotkeyHelp = "Click the box, then press the keys you want.";

    private readonly MainViewModel _vm = new();
    private readonly ProfileWatcher _watcher = new();
    private readonly DispatcherTimer _refreshDebounce;
    private readonly DispatcherTimer _saveDebounce;
    private readonly GlobalHotkey _hotkey;
    private readonly TrayIcon _tray;
    private bool _quitting;

    public MainWindow()
    {
        ThemeManager.Apply(_vm.Theme);
        InitializeComponent();
        DataContext = _vm;

        // Reflect the saved theme without treating it as a user change.
        (_vm.Theme switch
        {
            ThemeChoice.Light => ThemeLight,
            ThemeChoice.Dark => ThemeDark,
            _ => ThemeSystem
        }).IsChecked = true;

        // The browsers write Local State in bursts; wait for it to go quiet before re-reading.
        _refreshDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _refreshDebounce.Tick += (_, _) => { _refreshDebounce.Stop(); _vm.Refresh(); };
        _watcher.Changed += () => Dispatcher.BeginInvoke(new Action(() =>
        {
            _refreshDebounce.Stop();
            _refreshDebounce.Start();
        }));

        // The size slider changes TileSize dozens of times a second; write to disk once it rests.
        _saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveDebounce.Tick += (_, _) => { _saveDebounce.Stop(); _vm.Save(); };
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.TileSize)) return;
            _saveDebounce.Stop();
            _saveDebounce.Start();
        };

        _tray = new TrayIcon(Summon, Quit);

        _hotkey = new GlobalHotkey(this);
        _hotkey.Pressed += OnHotkey;
        if (!_hotkey.TryRegister(_vm.Hotkey))
            HotkeyStatus.Text = $"{_vm.HotkeyText} is in use by something else, so the hotkey is off. Pick another.";

        Activated += (_, _) => _vm.Refresh();          // fallback re-scan whenever the launcher comes forward
        Loaded += (_, _) => SearchBox.Focus();
    }

    // ---- showing, hiding, quitting ----------------------------------------------------------

    /// <summary>Bring the launcher forward, ready to type a client name.</summary>
    public void Summon()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;        // nudge past focus-stealing prevention
        Topmost = false;
        _vm.SearchText = "";
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    private void OnHotkey()
    {
        if (IsVisible && IsActive && _vm.CloseToTray) HideToTray();
        else Summon();
    }

    private void HideToTray()
    {
        Hide();
        if (_vm.TrayHintShown) return;
        _vm.TrayHintShown = true;
        _tray.ShowHint("Profile Launcher is still running",
                       $"Press {_vm.HotkeyText} to bring it back, or use the tray icon.");
    }

    private void Quit()
    {
        _quitting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_quitting && _vm.CloseToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _vm.Save();
        _hotkey.Dispose();
        _tray.Dispose();
        _watcher.Dispose();
        base.OnClosing(e);
        Application.Current.Shutdown();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;

        if (_vm.IsSettingsOpen) _vm.IsSettingsOpen = false;
        else if (_vm.SearchText.Length > 0) _vm.SearchText = "";
        else if (_vm.CloseToTray) HideToTray();
    }

    // ---- tiles ------------------------------------------------------------------------------

    /// <summary>Left-click: ask the environment once, then ask which portal to land on.</summary>
    private void Tile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TileViewModel tile) return;

        if (tile.Environment == TenantEnvironment.Unset)
        {
            var answer = EnvironmentPrompt.Ask(this, tile.Name);
            if (answer is null) return;
            _vm.SetEnvironment(tile, answer.Value);
        }

        var menu = new ContextMenu { PlacementTarget = element, Placement = PlacementMode.Bottom };
        menu.Items.Add(new MenuItem
        {
            Header = $"{tile.Name}  ·  {TileViewModel.EnvironmentLabel(tile.Environment)}",
            IsEnabled = false
        });
        menu.Items.Add(new Separator());

        foreach (var portal in _vm.Portals)
        {
            var item = new MenuItem { Header = portal.Name };
            item.Click += (_, _) => Launch(tile, portal);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    /// <summary>Right-click: exactly two options, with a check mark on whichever is set.</summary>
    private void Tile_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TileViewModel tile) return;
        e.Handled = true;

        var menu = new ContextMenu { PlacementTarget = element, Placement = PlacementMode.MousePoint };
        foreach (var env in new[] { TenantEnvironment.Gcch, TenantEnvironment.Commercial })
        {
            var item = new MenuItem
            {
                Header = TileViewModel.EnvironmentLabel(env),
                IsCheckable = true,
                IsChecked = tile.Environment == env
            };
            item.Click += (_, _) => _vm.SetEnvironment(tile, env);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    private void Star_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;      // the star sits inside the tile button; do not open the tile as well
        if (sender is FrameworkElement { DataContext: TileViewModel tile }) _vm.ToggleFavourite(tile);
    }

    private void Launch(TileViewModel tile, Portal portal)
    {
        try
        {
            _vm.Launch(tile, portal);
            if (_vm.HideAfterLaunch && _vm.CloseToTray) HideToTray();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open profile", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---- side panel and filters ---------------------------------------------------------------

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag }) _vm.ShowFavouritesOnly = tag == "Favourites";
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag }) _vm.BrowserFilter = tag;
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewProfileWindow(this);
        if (dialog.ShowDialog() != true) return;

        foreach (var browser in dialog.Browsers)
        {
            if (_vm.Exists(browser, dialog.ClientName))
            {
                var answer = MessageBox.Show(this,
                    $"There is already a {browser} profile called \"{dialog.ClientName}\". Create another one anyway?",
                    "Profile already exists", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) continue;
            }

            try
            {
                _vm.CreateProfile(browser, dialog.ClientName, dialog.Colour, dialog.Environment);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, $"Could not create the {browser} profile",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // The browser registers the new profile a moment after it opens; look again shortly.
        _refreshDebounce.Stop();
        _refreshDebounce.Start();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => _vm.IsSettingsOpen = !_vm.IsSettingsOpen;

    private void CloseSettings_Click(object sender, RoutedEventArgs e) => _vm.IsSettingsOpen = false;

    private void Quit_Click(object sender, RoutedEventArgs e) => Quit();

    // ---- settings -----------------------------------------------------------------------------

    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        if (!Enum.TryParse(tag, out ThemeChoice choice)) return;
        _vm.Theme = choice;
        ThemeManager.Apply(choice);
    }

    private void HotkeyBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        HotkeyBox.Text = "Press the keys…";
        HotkeyStatus.Text = "Hold Ctrl, Alt or Win, then press one more key. Esc cancels.";
    }

    private void HotkeyBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Setting Text above replaced the binding; put it back so the box shows the saved chord.
        HotkeyBox.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(MainViewModel.HotkeyText)) { Mode = System.Windows.Data.BindingMode.OneWay });
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            HotkeyStatus.Text = HotkeyHelp;
            SearchBox.Focus();
            return;
        }

        // Still holding modifiers, nothing to record yet.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.None or Key.ImeProcessed or Key.DeadCharProcessed)
            return;

        var pressed = Keyboard.Modifiers;
        uint mods = 0;
        if (pressed.HasFlag(ModifierKeys.Control)) mods |= HotkeySetting.ModControl;
        if (pressed.HasFlag(ModifierKeys.Alt)) mods |= HotkeySetting.ModAlt;
        if (pressed.HasFlag(ModifierKeys.Shift)) mods |= HotkeySetting.ModShift;
        if (pressed.HasFlag(ModifierKeys.Windows)) mods |= HotkeySetting.ModWin;

        // Shift on its own would hijack ordinary typing everywhere in Windows.
        if ((mods & (HotkeySetting.ModControl | HotkeySetting.ModAlt | HotkeySetting.ModWin)) == 0)
        {
            HotkeyStatus.Text = "Include Ctrl, Alt or Win so the hotkey cannot fire while you type.";
            return;
        }

        var candidate = new HotkeySetting { Modifiers = mods, VirtualKey = KeyInterop.VirtualKeyFromKey(key) };

        if (_hotkey.TryRegister(candidate))
        {
            _vm.Hotkey = candidate;
            HotkeyStatus.Text = $"Saved. {candidate.Display} now opens the launcher from anywhere.";
        }
        else
        {
            bool restored = _hotkey.TryRegister(_vm.Hotkey);
            HotkeyStatus.Text = restored
                ? $"{candidate.Display} is already taken by Windows or another app. Kept {_vm.HotkeyText}."
                : $"{candidate.Display} is already taken by Windows or another app.";
        }

        SearchBox.Focus();
    }
}
