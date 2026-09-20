using System.Windows.Media;
using ProfileLauncher.Models;

namespace ProfileLauncher.ViewModels;

/// <summary>One client tile: a browser profile plus what the launcher remembers about it.</summary>
public sealed class TileViewModel : ObservableObject
{
    private readonly ClientState _state;

    public TileViewModel(BrowserProfile profile, ClientState state)
    {
        Profile = profile;
        _state = state;

        var fill = new SolidColorBrush(profile.Color);
        fill.Freeze();
        Fill = fill;

        // Pick black or white initials, whichever reads better on the profile colour.
        double luminance = 0.299 * profile.Color.R + 0.587 * profile.Color.G + 0.114 * profile.Color.B;
        InitialsBrush = luminance > 160 ? Brushes.Black : Brushes.White;

        Initials = MakeInitials(profile.Name);
    }

    public BrowserProfile Profile { get; }
    public string Name => Profile.Name;
    public string Initials { get; }
    public Brush Fill { get; }
    public Brush InitialsBrush { get; }

    public TenantEnvironment Environment
    {
        get => _state.Environment;
        set
        {
            if (_state.Environment == value) return;
            _state.Environment = value;
            Raise();
            Raise(nameof(Tooltip));
        }
    }

    public bool IsFavourite
    {
        get => _state.Favourite;
        set
        {
            if (_state.Favourite == value) return;
            _state.Favourite = value;
            Raise();
        }
    }

    public DateTimeOffset? LastOpened
    {
        get => _state.LastOpened;
        set
        {
            _state.LastOpened = value;
            Raise();
            Raise(nameof(LastOpenedText));
        }
    }

    /// <summary>Empty until the tile has been opened once, so a fresh grid stays clean.</summary>
    public string LastOpenedText =>
        _state.LastOpened is { } t ? $"Last opened {t.ToLocalTime():g}" : "";

    public string Tooltip => $"{Name}  ·  {Profile.Browser}  ·  {EnvironmentLabel(Environment)}";

    public static string EnvironmentLabel(TenantEnvironment env) => env switch
    {
        TenantEnvironment.Gcch => "GCCH",
        TenantEnvironment.Commercial => "GCC/Commercial",
        _ => "environment not set"
    };

    /// <summary>Two words or more: first letter of the first two. One word: its first two characters.</summary>
    private static string MakeInitials(string name)
    {
        var words = name.Split(new[] { ' ', '-', '_', '.', '/' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(w => char.IsLetterOrDigit(w[0]))
                        .ToArray();

        string result = words.Length switch
        {
            0 => "?",
            1 => words[0].Length >= 2 ? words[0][..2] : words[0],
            _ => $"{words[0][0]}{words[1][0]}"
        };
        return result.ToUpperInvariant();
    }
}
