using System.Windows;
using Microsoft.Win32;
using ProfileLauncher.Models;

namespace ProfileLauncher.Services;

public static class ThemeManager
{
    private static ThemeChoice _choice = ThemeChoice.FollowSystem;

    static ThemeManager()
    {
        // Follow Windows live: if the user flips app mode while we are open, repaint.
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (_choice != ThemeChoice.FollowSystem) return;
            Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(_choice)));
        };
    }

    public static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Swaps theme slot 0 in App.xaml. Everything uses DynamicResource, so it repaints live.</summary>
    public static void Apply(ThemeChoice choice)
    {
        _choice = choice;
        bool dark = choice == ThemeChoice.Dark || (choice == ThemeChoice.FollowSystem && SystemPrefersDark());

        var dict = new ResourceDictionary
        {
            Source = new Uri(dark ? "/Themes/Dark.xaml" : "/Themes/Light.xaml", UriKind.Relative)
        };
        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0) merged[0] = dict; else merged.Add(dict);
    }
}
