using System.Windows;
using System.Windows.Controls;
using ProfileLauncher.Models;

namespace ProfileLauncher;

/// <summary>The one-time "which cloud is this client in?" question. Returns null if the dialog is closed.</summary>
public static class EnvironmentPrompt
{
    public static TenantEnvironment? Ask(Window owner, string clientName)
    {
        TenantEnvironment? result = null;

        var window = new Window
        {
            Title = "Set environment",
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.ToolWindow,
            FontFamily = owner.FontFamily
        };
        window.SetResourceReference(Control.BackgroundProperty, "WindowBg");

        var heading = new TextBlock
        {
            Text = clientName,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");

        var question = new TextBlock
        {
            Text = "Which environment is this client in? You will only be asked once.\n" +
                   "Right-click the tile to change it later.",
            Margin = new Thickness(0, 0, 0, 16)
        };
        question.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");

        Button Choice(string label, TenantEnvironment env, Thickness margin)
        {
            var button = new Button
            {
                Content = label,
                MinWidth = 150,
                Padding = new Thickness(12, 8, 12, 8),
                Margin = margin
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "PillButton");
            button.Click += (_, _) => { result = env; window.Close(); };
            return button;
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Choice("GCCH", TenantEnvironment.Gcch, new Thickness(0)));
        buttons.Children.Add(Choice("GCC/Commercial", TenantEnvironment.Commercial, new Thickness(8, 0, 0, 0)));

        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(heading);
        root.Children.Add(question);
        root.Children.Add(buttons);
        window.Content = root;

        window.ShowDialog();
        return result;
    }
}
