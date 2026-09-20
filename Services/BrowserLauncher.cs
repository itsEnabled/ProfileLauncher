using System.Diagnostics;
using Microsoft.Win32;
using ProfileLauncher.Models;

namespace ProfileLauncher.Services;

public static class BrowserLauncher
{
    /// <summary>Full path to chrome.exe / msedge.exe, or null if the browser is not installed.</summary>
    public static string? FindExe(BrowserKind browser)
    {
        string exe = browser == BrowserKind.Chrome ? "chrome.exe" : "msedge.exe";

        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = hive.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}");
                if (key?.GetValue(null) is string p)
                {
                    p = p.Trim('"');
                    if (File.Exists(p)) return p;
                }
            }
            catch
            {
                // fall through to the well-known locations
            }
        }

        string relative = browser == BrowserKind.Chrome
            ? @"Google\Chrome\Application\chrome.exe"
            : @"Microsoft\Edge\Application\msedge.exe";

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        return roots.Where(r => !string.IsNullOrEmpty(r))
                    .Select(r => Path.Combine(r, relative))
                    .FirstOrDefault(File.Exists);
    }

    /// <summary>Opens the URL in the given profile. Throws if the browser cannot be found or started.</summary>
    public static void Open(BrowserProfile profile, string url)
    {
        string exe = FindExe(profile.Browser)
                     ?? throw new FileNotFoundException($"{profile.Browser} does not appear to be installed.");

        var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
        psi.ArgumentList.Add($"--profile-directory={profile.Folder}");   // ArgumentList handles the quoting
        psi.ArgumentList.Add(url);
        Process.Start(psi)?.Dispose();
    }
}
