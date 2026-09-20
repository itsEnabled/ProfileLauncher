namespace ProfileLauncher.Services;

/// <summary>
/// System tray presence so the launcher can stay resident for the hotkey.
/// Windows Forms types are fully qualified on purpose; that namespace is not imported anywhere.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;

    public TrayIcon(Action open, Action quit)
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open Profile Launcher", null, (_, _) => open());
        menu.Items.Add("Quit", null, (_, _) => quit());

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Profile Launcher",
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left) open();
        };
    }

    public void ShowHint(string title, string text) =>
        _icon.ShowBalloonTip(4000, title, text, System.Windows.Forms.ToolTipIcon.Info);

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe is not null && System.Drawing.Icon.ExtractAssociatedIcon(exe) is { } icon) return icon;
        }
        catch
        {
            // fall through
        }
        return System.Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
