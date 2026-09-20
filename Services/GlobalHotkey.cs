using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ProfileLauncher.Models;

namespace ProfileLauncher.Services;

/// <summary>A system-wide hotkey delivered to the window's message loop, even while it is hidden.</summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4C50;
    private const uint ModNoRepeat = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _handle;
    private readonly HwndSource? _source;
    private bool _registered;

    public event Action? Pressed;

    public GlobalHotkey(Window window)
    {
        _handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);
    }

    /// <summary>False means Windows or another app already owns that combination.</summary>
    public bool TryRegister(HotkeySetting hotkey)
    {
        Unregister();
        _registered = RegisterHotKey(_handle, HotkeyId, hotkey.Modifiers | ModNoRepeat, (uint)hotkey.VirtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        UnregisterHotKey(_handle, HotkeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
    }
}
