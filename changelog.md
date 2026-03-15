# Changelog

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
