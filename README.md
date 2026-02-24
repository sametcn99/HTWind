# HTWind

HTWind is a .NET 10 WPF desktop app for running HTML widgets directly on your Windows desktop.
It combines a fluent host UI, a widget manager, and a lightweight host bridge so widgets can call selected Windows-side actions.

## Highlights

- Desktop HTML widgets with lock/unlock interaction modes
- Built-in widget library (clock, weather, system tools, file helpers, and more)
- Widget code editor with live preview (hot reload)
- Tray integration (show/hide app, background workflow)
- Pin-on-top, visibility toggle, and persisted widget geometry/state
- Startup toggle (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`)
- Localization infrastructure (`resx` + `LocExtension`)

## Built-In Widgets

HTWind currently ships with these built-in templates:

- `clock`
- `weather`
- `tictactoe`
- `system-monitor`
- `app-launcher`
- `visualizer`
- `search-box`
- `quick-links`
- `clipboard-studio`
- `system-time`
- `memory-stats`
- `environment-info`
- `network-tools`
- `process-manager`
- `file-explorer`
- `text-file-editor`
- `app-info`
- `media-controls`
- `dns-lookup`
- `file-actions`
- `drive-roots`
- `powershell-console`

Template source files live in `HTWind/Templates`.

## Installation

### Option 1: From GitHub Releases (recommended)

1. Open `Releases` in this repository.
2. Download one of the assets:

- `HTWind-setup-<version>.exe` (installer)
- `HTWind-portable-<version>.zip` (portable)

1. For installer mode, run the setup executable and follow the wizard.

### Option 2: Run from source

Prerequisites:

- Windows 10/11
- .NET SDK 10.0+

Commands:

```powershell
dotnet restore HTWind/HTWind.csproj
dotnet build HTWind/HTWind.csproj
dotnet run --project HTWind/HTWind.csproj
```

## Uninstallation

If you are using the installed version of HTWind, you can uninstall it from **Windows Settings > Apps > Installed apps**.

> [!IMPORTANT]
> Uninstalling HTWind will delete all widgets and data stored in `%LocalAppData%\HTWind`. If you are uninstalling the app to perform a clean update, make sure to take a backup of this folder before proceeding.

## How To Use

1. Launch HTWind.
2. Add a widget (`Add Widget`) from an HTML file.
3. Use per-widget controls:

- `Visible` to show/hide
- `Locked` to switch between interaction and move/resize mode
- `Pin on top` to keep above other windows
- `Edit` to open the code editor

1. Use tray icon actions for quick show/exit behavior.

## Widget Development

You can build custom widgets using plain HTML/CSS/JavaScript.

### Host Bridge API

Widgets can call:

- `window.HTWind.invoke("powershell.exec", args)`

Supported args include:

- `script` (required)
- `timeoutMs`
- `maxOutputChars`
- `shell` (`powershell` or `pwsh`)
- `workingDirectory`

Important:

- Only `powershell.exec` is currently supported.
- Output is clipped by `maxOutputChars` for safety.
- Scripts are executed with `-NoProfile -NonInteractive -ExecutionPolicy Bypass`.

### Security and Responsibility Notice

- HTWind allows widgets to execute PowerShell commands via `powershell.exec`.
- Running commands can modify files, processes, registry entries, and network/system settings.
- All command execution risk is owned by the user running HTWind.
- On first launch, HTWind requires explicit acceptance of this risk before the app opens.

## Share Widgets Via GitHub Discussions

Use repository Discussions to share reusable widgets with the community.

Suggested post format:

1. Title: `[Widget] <name>`
2. Summary: what it does
3. Preview: screenshot or short GIF
4. Code: attach `.html` file or paste source
5. Notes: permissions, external APIs, and known limitations
6. Version: compatible HTWind version

Suggested categories:

- `Widget Showcase`
- `Widget Requests`
- `Widget Help`

Users can copy the shared HTML file and add it through `Add Widget` inside HTWind.

## Contributing

Contributions are welcome.

1. Fork the repository
2. Create a feature branch
3. Make focused changes
4. Build, run, and validate locally
5. Open a Pull Request with a clear description

### Local Development

#### Option 1: Terminal Commands

Use these commands from the repository root:

```powershell
dotnet restore HTWind/HTWind.csproj
dotnet build HTWind/HTWind.csproj
dotnet run --project HTWind/HTWind.csproj
```

Optional cleanup:

```powershell
dotnet clean HTWind/HTWind.csproj
```

#### Option 2: VS Code Packaging Tasks

You can run the same workflow from VS Code task runner.

Available tasks:

- `restore HTWind`
- `build HTWind`
- `run HTWind`
- `clean HTWind`

Run via:

1. `Terminal` -> `Run Task...`
2. Select one of the tasks above

### Creating Local Packages

#### Option 1: Terminal Scripts

Create installer:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-installer.ps1
```

Create portable zip:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-portable.ps1
```

#### Option 2: VS Code Tasks

Available packaging tasks:

- `package HTWind installer (local)`
- `package HTWind portable (local)`
- `package HTWind all (local)`

Run via:

1. `Terminal` -> `Run Task...`
2. Choose the package task you need

Before opening a PR, at minimum run:

```powershell
dotnet build HTWind/HTWind.csproj
```

### Contribution areas

- New built-in widgets
- Host API improvements
- UI/UX polish for WPF screens
- Stability/performance fixes
- Documentation and localization

## Adding A New Translation

HTWind already uses `Resources/Strings.resx` and `LocalizationService`.

To add a new language:

1. Create a culture-specific resource file in `HTWind/Resources`:

- Example: `Strings.tr-TR.resx`

1. Copy all keys from `Strings.resx`.
2. Translate only values (do not change keys).
3. Keep placeholders and formatting tokens unchanged.
4. Build and test.

Current startup culture is set in `HTWind/App.xaml.cs`:

- `LocalizationService.SetCulture("en-US");`

For local testing, temporarily switch that value to your new culture (for example `tr-TR`) and run the app.

## Releases

This repository includes automated release workflow at:

- `.github/workflows/release.yml`

When you push a tag like `v1.0.0`, GitHub Actions builds:

- installer (`HTWind-setup-*.exe`) via Inno Setup
- portable archive (`HTWind-portable-*.zip`)

Both are uploaded to the GitHub Release page.

## Support

- Issues: <https://github.com/sametcn99/HTWind/issues>
- Discussions: <https://github.com/sametcn99/HTWind/discussions>

If you find a bug, please include reproduction steps, expected behavior, and environment details.

## License

This project is licensed under the GPL-3.0 License. See the [LICENSE](LICENSE) file for details.
