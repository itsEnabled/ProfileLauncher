# Profile Launcher

A Windows launcher for Chrome and Edge client profiles. Profiles are discovered
automatically from each browser's `Local State` file.

## Build and run

Needs the .NET 8 SDK (build machine only; the published exe needs nothing):

    winget install Microsoft.DotNet.SDK.8

From this folder:

    dotnet run

If a previous copy is still running in the tray, quit it first (tray icon > Quit),
otherwise the new one just brings the old one forward and exits.

## Publish the single exe

Double-click `publish.cmd`. It builds the release and leaves exactly one file to share:

    dist\ProfileLauncher.exe

Self-contained, roughly 60 to 80 MB, no installer, nothing else needed. Do not take the
exe from `bin\...` by hand: the folder above `publish` holds a small look-alike that
cannot run on its own.

## Releases on GitHub

The workflow in `.github/workflows/build.yml` builds the exe on GitHub's Windows runners.
To cut a release, tag the commit and push the tag:

    git tag v0.5.1
    git push origin v0.5.1

A few minutes later the repo's Releases page has `ProfileLauncher.exe` plus its SHA-256.

## Handing it to people

They download the exe, put it anywhere, and run it. Settings live in
`%APPDATA%\ProfileLauncher`, so the exe can be moved or replaced freely. To update someone,
give them the new exe to overwrite the old one (quit from the tray first).

A downloaded, unsigned exe triggers Windows SmartScreen the first time ("Windows protected
your PC" > More info > Run anyway). Code-signing the exe removes that prompt.

## Portable vs AppData

- Bare exe: config is kept in `%APPDATA%\ProfileLauncher`.
- Portable: put an empty file named `state.json` next to the exe. The launcher
  then keeps `state.json` and `portals.json` in that folder instead.

The Settings panel shows which folder is in use.

## Files it writes

- `state.json`: per-client environment, favourites, last-opened timestamps,
  icon size, theme, hotkey, behaviour options.
- `portals.json`: the portal list with a Commercial and a Gcch URL for each.
  Written with defaults on first run. Edit it freely; changes load on next start.

## Features

Discovery with live refresh, tile grid in profile colours, search, Chrome/Edge
filter, first-click environment prompt, left-click portal picker, right-click
GCCH / GCC/Commercial toggle, last-opened line, favourites (hover star + side
panel), Settings panel (hotkey capture, live icon-size slider,
light/dark/system theme), global hotkey, tray icon, start with Windows, single instance, new-profile
creation.

Default hotkey: Ctrl + Alt + Space. Esc clears the search, then hides the window.

## New profile

Side panel > New profile. Pick a name, environment, browser(s) and colour. The launcher
creates the profile folder (`PL-ClientName` under the browser's User Data), seeds it with
the name and colour, then opens it so the browser registers it. The tile appears a moment later. To remove a profile, delete it
from the browser's own profile manager as usual.

## Still to come

Portal bookmarks inside each profile (deferred: a browser that is
already running ignores edits to its bookmarks file, so this needs a different approach).
