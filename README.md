# HTWind

HTWind is a highly customizable, HTML-based widget manager that brings your favorite web tools and system helpers directly to your Windows desktop.
It also supports running PowerShell commands when you need quick system actions.

[Website](https://htwind.vercel.app)

<a href="https://apps.microsoft.com/detail/9PN58CG1P20L?referrer=appbadge&cid=sametcn99&mode=full" target="_blank"  rel="noopener noreferrer">
 <img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200"/>
</a>

## Screenshots

<p align="center">
 <img src="assets/app_screenshots/page_home.png" width="600" alt="Home - Widget Library"/>
 <br/>
 <i>Widget Library - Manage and toggle HTML widgets</i>
</p>

<p align="center">
 <img src="assets/app_screenshots/page_settings.png" width="600" alt="Settings - App Customization"/>
 <br/>
 <i>Settings - Theme and startup configuration</i>
</p>

<p align="center">
 <img src="assets/app_screenshots/page_about.png" width="600" alt="About - Version Information"/>
 <br/>
 <i>About - Version info and project links</i>
</p>

## Highlights

- **Native PowerShell script execution support for system automation and quick tasks**
- Desktop HTML widgets with lock/unlock interaction modes
- Import widget packages through `htwind.widget.json`
- Support for widgets with local assets such as CSS, JavaScript, images, and fonts
- Support for multi-widget packages that install several widgets from one manifest
- Built-in widget library (clock, weather, system tools, file helpers, and more)
- **Widget built-in code editor with live preview (hot reload)**
- Tray integration (show/hide app, background workflow)
- Pin-on-top, visibility toggle, and persisted widget geometry/state
- Smart visibility suppression by display: hide widget windows while another app is fullscreen (toggle)
- Optional maximized-window suppression by display (separate toggle from fullscreen suppression)
- Startup toggle (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`)
- Localization infrastructure (`resx` + `LocExtension`)
- Built-in code editor with syntax highlighting and live preview (hot reload)
- Open-source and community-driven development

## Visibility Suppression Modes

HTWind includes per-display runtime suppression options in **Settings** to reduce distraction and improve performance while other apps are in focus.

- **Hide widgets on fullscreen apps** (enabled by default):
Widget windows are temporarily closed on the same display when another app enters fullscreen, then restored when fullscreen ends.
- **Hide widgets on maximized apps** (optional):
Widget windows are temporarily closed on the same display when another app is maximized, then restored when that app is no longer maximized.

Notes:

- These options do not change the widget `Visible` state in app data.
- Suppression is runtime-only and windows are restored automatically.

## Share Widgets and Feedback With The Community

Use GitHub Discussions and the HTWind Reddit community to share reusable widgets, desktop setups, bug reports, and feature requests.

- GitHub Discussions: <https://github.com/sametcn99/HTWind/discussions>
- Reddit: <https://www.reddit.com/r/HTWind/>

## Installation

Recommended for most users: install HTWind using the installer executable from GitHub Releases (`HTWind-setup-<version>.exe`).
Portable ZIP and Microsoft Store installation are available as alternatives.

### Option 1: From GitHub Releases (recommended)

1. Open `Releases` in this repository.
2. Download one of the assets:

- `HTWind-setup-<version>.exe` (installer, recommended)
- `HTWind-portable-<version>.zip` (portable alternative)

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

## Widget Development

You can build custom widgets using plain HTML/CSS/JavaScript.

HTWind now supports three practical widget packaging levels:

- A single HTML file for the fastest possible prototype
- A widget folder with additional local assets such as CSS, JavaScript, images, or fonts
- A manifest-driven package using `htwind.widget.json`, which can describe one widget or multiple widgets in one importable bundle

### Widget Package Manifests

Use `htwind.widget.json` when your widget is made of more than one file or when you want to distribute multiple widgets together.

This package format is useful when you want to:

- Keep your widget code split across `index.html`, `styles.css`, `app.js`, and other local assets
- Ship several related widgets as one reusable desktop toolkit
- Export a single managed widget from HTWind as a portable package
- Export the current workspace as a manifest-based bundle for backup or team sharing

HTWind includes a schema and an example package in the repository:

- Schema: [HTWind/Templates/htwind.widget.schema.json](HTWind/Templates/htwind.widget.schema.json)
- Example multi-widget package: [HTWind/Templates/examples/multi-widget-package/htwind.widget.json](HTWind/Templates/examples/multi-widget-package/htwind.widget.json)

### Manifest Structure

The manifest describes package metadata and a `widgets` array. Each widget entry points to a relative folder, an entry HTML file, and the additional asset files that belong to that widget.

Typical fields:

- `schemaVersion`: currently `1`
- `name`: package name
- `description`, `author`, `version`, `homepage`: optional package metadata
- `widgets`: one or more widget entries

Each widget entry includes:

- `widgetId`: stable widget identifier inside the package
- `name`: display-oriented widget name
- `relativePath`: folder path relative to the manifest root
- `entryFile`: the widget HTML entry point
- `assets`: non-entry files that should travel with the widget

Example:

```json
{
  "$schema": "../../htwind.widget.schema.json",
  "schemaVersion": 1,
  "name": "multi-widget-package",
  "description": "Example package that contains two widgets in a single manifest.",
  "widgets": [
    {
      "widgetId": "status-board-widget",
      "name": "status-board",
      "relativePath": "widgets/status-board",
      "entryFile": "index.html",
      "assets": ["styles.css", "app.js"]
    },
    {
      "widgetId": "focus-timer-widget",
      "name": "focus-timer",
      "relativePath": "widgets/focus-timer",
      "entryFile": "index.html",
      "assets": ["styles.css", "app.js"]
    }
  ]
}
```

### Recommended Folder Layout

For widgets that use multiple files, keep the manifest at the package root and place each widget in its own folder.

```text
my-widget-package/
├─ htwind.widget.json
└─ widgets/
   ├─ status-board/
   │  ├─ index.html
   │  ├─ styles.css
   │  └─ app.js
   └─ focus-timer/
      ├─ index.html
      ├─ styles.css
      └─ app.js
```

This structure keeps packages predictable, easy to validate, and easy to export or import again later.

### Import and Export Workflow

HTWind supports both creator and consumer workflows:

- Import a standalone HTML file when you only need a simple single-file widget
- Import `htwind.widget.json` when the widget depends on local assets or the package includes multiple widgets
- Export a widget from the widget context menu to create a reusable package folder
- Export the current workspace from Settings to create a package-oriented backup of your active widget setup

When HTWind imports a manifest package, it copies the widget content into managed storage and preserves the package structure needed for the widget entry file and assets to work together.

### When to Use Single Files vs Manifests

Use a single HTML file when:

- You are prototyping quickly
- The widget is small and self-contained
- You want the simplest sharing format possible

Use `htwind.widget.json` when:

- Your widget depends on local CSS or JavaScript files
- You want to include images, icons, or other static assets
- You need to share several widgets together
- You want a package that is easier to evolve, validate, and re-export

### Generate Widgets With LLM Help

If you want AI assistance while creating widgets, use the dedicated HTWind system prompt:

- [HTWind Widget Generator Prompt](chat.prompt.md)
- [HTWind Widget Generator Prompt (shared)](https://prompts.chat/prompts/cmm5broas0004li04ku12tp56_htwind-widget-creator)

You can copy this prompt into your preferred LLM and ask it to generate HTWind-compatible widgets (including PowerShell bridge usage) as a single HTML file.

For larger widgets, a good workflow is to start with a single-file prototype, then split the widget into `index.html`, `styles.css`, `app.js`, and other assets before adding a `htwind.widget.json` manifest for packaging and sharing.

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

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full contribution guide.

## Support

- Issues: <https://github.com/sametcn99/HTWind/issues>
- Discussions: <https://github.com/sametcn99/HTWind/discussions>
- Reddit community: <https://www.reddit.com/r/HTWind/>

If you find a bug, please include reproduction steps, expected behavior, and environment details.

## License

This project is licensed under the GPL-3.0 License. See the [LICENSE](LICENSE) file for details.
