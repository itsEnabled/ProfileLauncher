using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;
using ProfileLauncher.Models;

namespace ProfileLauncher.Services;

/// <summary>
/// Creates a browser profile without going through the browser's own setup flow.
///
/// A Chromium profile is just a folder under "User Data". We make the folder and seed it with a
/// Preferences file (name and colour), then the caller launches the browser into it. On that first
/// launch the browser adopts the folder and registers it in Local State, and the tile appears.
///
/// Folders are named "PL-ClientName" rather than "Profile N" so they can never collide with the
/// numbering the browser uses for profiles it creates itself.
/// </summary>
public static class ProfileCreator
{
    /// <summary>Longest client name accepted. The dialog enforces the same number on the text box.</summary>
    public const int MaxClientNameLength = 60;

    private const int MaxFolderStemLength = 40;
    private const int MaxFolderAttempts = 500;
    private const string FolderPrefix = "PL-";

    /// <summary>
    /// The single place a client name is validated. Trims it, collapses runs of whitespace, and
    /// rejects anything empty, too long, or containing control characters. Returns false with a
    /// message the UI can show; Create calls this too, so the rule holds whoever the caller is.
    /// </summary>
    public static bool TryNormalizeName(string? raw, out string name, out string error)
    {
        name = "";
        error = "";

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Enter a client name.";
            return false;
        }
        if (raw.Length > MaxClientNameLength * 4)            // refuse before doing any work on it
        {
            error = $"Client names can be at most {MaxClientNameLength} characters.";
            return false;
        }
        if (raw.Any(char.IsControl))
        {
            error = "Client names cannot contain control characters.";
            return false;
        }

        name = string.Join(' ', raw.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length > MaxClientNameLength)
        {
            name = "";
            error = $"Client names can be at most {MaxClientNameLength} characters.";
            return false;
        }
        return true;
    }

    public static BrowserProfile Create(BrowserKind browser, string clientName, Color color)
    {
        if (!TryNormalizeName(clientName, out string validName, out string nameError))
            throw new ArgumentException(nameError, nameof(clientName));
        clientName = validName;

        string userData = ProfileDiscovery.UserDataDir(browser);
        if (!Directory.Exists(userData))
            throw new DirectoryNotFoundException(
                $"{browser} has not been run on this Windows account yet, so it has nowhere to keep profiles. " +
                $"Open {browser} once, then try again.");

        string folder = CreateUniqueFolder(userData, clientName);
        string path = Path.Combine(userData, folder);

        File.WriteAllText(Path.Combine(path, "Preferences"),
                          Preferences(clientName, color).ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                          new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new BrowserProfile(browser, folder, clientName, color);
    }

    /// <summary>
    /// Builds a folder name from the (already validated) client name and creates it.
    /// Only ASCII letters and digits survive; everything else becomes '-', so separators, dots,
    /// colons and ".." can never reach the path. Length and attempts are both bounded.
    /// </summary>
    private static string CreateUniqueFolder(string userData, string clientName)
    {
        var clean = new StringBuilder(clientName.Length);
        foreach (char ch in clientName)
            clean.Append(char.IsAsciiLetterOrDigit(ch) ? ch : '-');

        string stem = FolderPrefix + clean.ToString().Trim('-');
        if (stem.Length > MaxFolderStemLength) stem = stem[..MaxFolderStemLength].TrimEnd('-');
        if (stem == FolderPrefix) stem = FolderPrefix + "Client";

        string root = Path.GetFullPath(userData).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        for (int attempt = 1; attempt <= MaxFolderAttempts; attempt++)
        {
            string folder = attempt == 1 ? stem : $"{stem}-{attempt}";
            string path = Path.GetFullPath(Path.Combine(userData, folder));

            // Defense in depth: the sanitizing above already guarantees this, so failing here means a bug.
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to create a profile folder outside the browser's User Data directory.");

            if (Directory.Exists(path)) continue;

            Directory.CreateDirectory(path);
            return folder;
        }

        throw new IOException($"Could not find a free folder name for \"{clientName}\" after {MaxFolderAttempts} attempts.");
    }

    private static JsonObject Preferences(string clientName, Color color)
    {
        // Chromium stores colours as signed 32-bit ARGB.
        int argb = unchecked((int)(0xFF000000u | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B));

        return new JsonObject
        {
            ["profile"] = new JsonObject
            {
                ["name"] = clientName,
                ["using_default_name"] = false
            },
            // The colour pref has been renamed across Chromium versions; unknown keys are ignored,
            // so writing all of them is harmless and covers old and new builds.
            ["browser"] = new JsonObject
            {
                ["theme"] = new JsonObject
                {
                    ["user_color"] = argb,
                    ["user_color2"] = argb
                }
            },
            ["autogenerated"] = new JsonObject
            {
                ["theme"] = new JsonObject { ["color"] = argb }
            },
            ["extensions"] = new JsonObject
            {
                ["theme"] = new JsonObject { ["id"] = "user_color_theme_id" }
            }
        };
    }
}
