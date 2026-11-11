# Changelog

## v0.3.0

### Added

- Added a Gallery page for discovering and installing built-in and community widgets.
- Added opt-in PowerShell script execution for widgets with a dedicated permission flow and a read-only sandboxed engine.
- Added a bundled Monaco-based HTML code editor with virtual-host serving for widget authoring.
- Added workspace profiles so the current widget layout can be captured, restored, and shared.
- Added a per-widget settings window plus per-widget opacity and zoom controls.
- Added new built-in widgets: media controls, memory stats, system monitor, and audio visualizer.
- Added a collapsible details-style widget list on the Home page; widget settings stay hidden until the header is clicked.
- Added a full test stack: .NET unit tests (153), runtime tests covering the PowerShell sandbox, the WebView2 host bridge, all built-in templates, and the Monaco editor (104), in-process desktop E2E tests, web unit/i18n/SEO contract tests (119), Playwright web E2E tests, and CI quality gates with coverage reporting.
- Added a `tools/HTWind.ScreenshotCapture` utility to regenerate the marketing screenshots.

### Improved

- Improved the landing site with 18 locales, RTL support, structured data, and expanded accessibility.
- Improved widget storage, export, and template upgrade reliability (see fixes below).
- Refreshed the app screenshots to reflect the redesigned Home page.

### Fixed

- Fixed the PowerShell execution engine so the read-only cmdlet allowlist actually exists on machines without a PowerShell 7 install, and so a timing-out script can no longer hang the engine indefinitely.
- Fixed widget `storage.set` double-encoding values, which broke round-tripping JSON data from widgets.
- Fixed the code editor failing to load and save documents in bundled-Monaco mode.
- Fixed duplicate `og:locale:alternate` meta tags accumulating on locale switches.
- Fixed single-widget export packages failing to re-import due to a `relativePath: "."` resolution bug.

## v0.2.0

### Added

- Added manifest-based widget package support through `htwind.widget.json`.
- Added support for importing packages that contain multiple widgets from a single manifest.
- Added support for asset-backed widgets that ship with local CSS, JavaScript, images, fonts, and other static files.
- Added widget export support so a managed widget can be packaged and shared more easily.
- Added workspace export support so the current widget setup can be exported as a package-oriented bundle.
- Added the `htwind.widget.schema.json` schema file to document and validate the manifest structure.
- Added an example multi-widget package under `HTWind/Templates/examples/multi-widget-package`.

### Improved

- Improved widget import workflows so standalone HTML widgets and manifest packages can be handled through the same application flow.
- Improved package portability by explicitly modeling widget entry files, relative paths, and declared assets.
- Improved permission identity handling for packaged widgets so decisions can be associated with the widget package root.

### Documentation

- Expanded the README with detailed English documentation for single-file widgets, asset-backed widgets, and multi-widget manifest packages.
- Added a dedicated landing page section that explains manifest packages, schema usage, import/export workflows, and package structure.
