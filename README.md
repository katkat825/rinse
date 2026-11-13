# Rinse

**Your clipboard, minus the junk.**

Rinse is a small Windows tray app. It cleans up links as you copy them and gives you one-click fixes for messy text. It runs entirely on your computer: no accounts, no network, no telemetry.

## What it does

### Cleans links automatically

When you copy a link, Rinse cleans it before you paste:

- **Unwraps redirect wrappers** back to the real address: Outlook SafeLinks, Proofpoint (urldefense), Google, Facebook, Instagram, Reddit, YouTube, Slack and LinkedIn redirects.
- **Strips tracking parameters** such as `utm_*`, `fbclid`, `gclid`, `msclkid`, `mc_eid`, YouTube `si=` and Amazon `ref=` junk.
- **Leaves meaningful parameters alone**, like `?v=`, `?t=`, `?id=` and GitHub's `?ref=`.

Before:

```
https://nam12.safelinks.protection.outlook.com/?url=https%3A%2F%2Fwww.example.com%2Fwebinar%3Fid%3D42%26utm_source%3Dnewsletter%26utm_medium%3Demail%26fbclid%3DIwAR0abc&data=05%7C02%7C&sdata=xyz%3D&reserved=0
```

After:

```
https://www.example.com/webinar?id=42
```

A small notice tells you what was removed. If you need the original back, choose **Undo last rinse** from the tray menu.

### One-click text fixes

Press **Win+Alt+V** (or click the tray icon) to open the panel. Every tool puts its result straight back on your clipboard.

| Group | Tools |
| --- | --- |
| Clean | Plain text, Fix line breaks (for text copied from PDFs), Tidy spaces, Fix quotes, Rinse links |
| Case | UPPER, lower, Title Case, Sentence case |
| Lines | Sort A→Z, Remove duplicates, Remove blank lines, Undo |

Shortcuts: **Ctrl+Enter** copies the text, **Esc** closes the panel.

The panel also shows your last 25 copies so you can put an older one back on the clipboard.

## Privacy

- Recent copies are kept in memory only and are never written to disk. They're gone when Rinse quits.
- Anything a password manager marks as private on the clipboard is ignored completely.
- Rinse makes no network requests.
- The only things it saves are its settings and your "links rinsed" counter, in `%AppData%\Rinse\settings.json`.

## Install and run

**Requirements:** Windows 10 or 11 and the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (x64).

1. Download `Rinse.exe` from the [Releases](../../releases) page, or build it yourself (see below).
2. Run it. A teal drop appears in your system tray.
3. Copy a link and paste it somewhere. That's it.

There's no installer. To remove Rinse: turn off **Start with Windows** if you turned it on, quit from the tray menu, then delete the exe (and the `%AppData%\Rinse` folder if you want the settings gone too).

### Tray menu

- **Open Rinse:** opens the text tools panel.
- **Undo last rinse:** puts the original, uncleaned link back on the clipboard.
- **Auto-rinse copied links:** turns automatic link cleaning on or off.
- **Show a notice when a link is rinsed:** turns the notifications on or off.
- **Start with Windows:** off by default. When you turn it on, Rinse adds itself to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, and removes that entry when you turn it off.
- **Quit Rinse.**

If another app already uses Win+Alt+V, Rinse falls back to **Ctrl+Alt+Shift+V**. The tray menu shows which shortcut is active.

## Build from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

Double-click `build.cmd`, or run:

```
cd src
dotnet publish Rinse.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ../app
```

The finished app is written to `app\Rinse.exe`. `build.cmd` also runs the self-tests and writes the results to `selftest.txt`.

### Self-tests

The link-cleaning and text tools have built-in tests:

```
app\Rinse.exe --selftest results.txt
```

The exit code is 0 when every test passes.

### Project layout

```
src/
  Program.cs        entry point, single-instance check
  TrayApp.cs        tray icon, menu, clipboard watching, auto-rinse
  PanelForm.cs      the text tools window
  LinkCleaner.cs    redirect unwrapping and tracking-parameter rules
  TextTools.cs      line-break, spacing, quote, case and line tools
  ClipboardBits.cs  clipboard access, hotkey and in-memory history
  Settings.cs       settings file and the Start with Windows option
  Theme.cs          colors, fonts and icon
  SelfTest.cs       built-in tests
```

Plain WinForms on .NET 9, with no NuGet packages.

## License

[MIT](LICENSE). You're free to use, change and share Rinse, including commercially, as long as you keep the copyright notice.
