using System.Windows;
using ProfileLauncher.Services;

namespace ProfileLauncher;

public partial class App : Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One instance only. Running the exe again just brings the existing launcher forward,
        // which is also the way back in if the hotkey ever fails to register.
        _mutex = new Mutex(true, @"Local\ProfileLauncher.Instance", out bool isFirst);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ProfileLauncher.Show");

        if (!isFirst)
        {
            _showSignal.Set();
            Shutdown();
            return;
        }

        // Launched by Windows at sign-in: sit in the tray and wait for the hotkey instead of popping up.
        bool startHidden = e.Args.Any(a => string.Equals(a, StartupManager.TrayArgument, StringComparison.OrdinalIgnoreCase));

        _window = new MainWindow();
        MainWindow = _window;
        if (!startHidden) _window.Show();

        StartupManager.RefreshPath();

        var signal = _showSignal;
        var listener = new Thread(() =>
        {
            while (signal.WaitOne())
                Dispatcher.BeginInvoke(new Action(() => _window?.Summon()));
        })
        { IsBackground = true, Name = "ShowSignalListener" };
        listener.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _mutex?.ReleaseMutex(); } catch { /* not the owner: second instance */ }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
