using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProfileLauncher.Models;
using ProfileLauncher.Services;

namespace ProfileLauncher;

public partial class NewProfileWindow : Window
{
    private static readonly string[] Palette =
    {
        "#1A73E8", "#D93025", "#188038", "#F29900", "#9334E6",
        "#E52592", "#12B5CB", "#5F6368", "#795548", "#3F51B5"
    };

    public NewProfileWindow(Window owner)
    {
        Owner = owner;
        InitializeComponent();

        foreach (string hex in Palette)
        {
            var swatch = new RadioButton
            {
                GroupName = "Colour",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                Style = (Style)FindResource("Swatch")
            };
            Swatches.Children.Add(swatch);
        }
        ((RadioButton)Swatches.Children[0]).IsChecked = true;

        // Only offer browsers that are actually installed; tick Chrome by default, else Edge.
        bool chrome = BrowserLauncher.FindExe(BrowserKind.Chrome) is not null;
        bool edge = BrowserLauncher.FindExe(BrowserKind.Edge) is not null;
        InChrome.IsEnabled = chrome;
        InEdge.IsEnabled = edge;
        InChrome.IsChecked = chrome;
        InEdge.IsChecked = !chrome && edge;

        NameBox.MaxLength = ProfileCreator.MaxClientNameLength;      // one number, owned by the creator
        Loaded += (_, _) => NameBox.Focus();
    }

    /// <summary>The validated name, or empty if the box does not currently hold a valid one.</summary>
    public string ClientName =>
        ProfileCreator.TryNormalizeName(NameBox.Text, out string name, out _) ? name : "";

    public TenantEnvironment Environment =>
        EnvGcch.IsChecked == true ? TenantEnvironment.Gcch : TenantEnvironment.Commercial;

    public List<BrowserKind> Browsers
    {
        get
        {
            var list = new List<BrowserKind>();
            if (InChrome.IsChecked == true) list.Add(BrowserKind.Chrome);
            if (InEdge.IsChecked == true) list.Add(BrowserKind.Edge);
            return list;
        }
    }

    public Color Colour
    {
        get
        {
            foreach (RadioButton swatch in Swatches.Children)
                if (swatch.IsChecked == true && swatch.Background is SolidColorBrush brush)
                    return brush.Color;
            return (Color)ColorConverter.ConvertFromString(Palette[0]);
        }
    }

    private const string DefaultNote =
        "The profile opens straight away so the browser can register it. Its tile appears a moment later.";

    private void UpdateCreateButton()
    {
        bool valid = ProfileCreator.TryNormalizeName(NameBox.Text, out _, out string error);
        // Say nothing while the box is simply empty; only explain a name that was typed and rejected.
        Note.Text = valid || NameBox.Text.Length == 0 ? DefaultNote : error;
        CreateButton.IsEnabled = valid && Browsers.Count > 0;
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCreateButton();

    private void Browser_Click(object sender, RoutedEventArgs e) => UpdateCreateButton();

    private void Create_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
