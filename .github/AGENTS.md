# HTWind Agent Guide

This file is a quick reference for coding agents working in this repository.

## Goal
HTWind is a WPF desktop app that runs with a system tray icon and manages HTML-based widget windows. Agents must apply changes without breaking these core behaviors.

## Technical Environment
- `.NET 10` + `WPF`
- Packages: `WPF-UI`, `WebView2`, `Hardcodet.NotifyIcon.Wpf`
- Localization: `Resources/Strings.resx` + `LocalizationService`

## Architectural Boundaries
- As UI-side business logic grows, move it to the service layer.
- Service dependencies should flow through interfaces (`IWidgetManager`, `IThemeService`, etc.).
- File/registry/OS access should be abstracted (add new `I*Service` where needed).

## WPF App Rules
- Do not run blocking work synchronously in event handlers.
- Manage event subscription lifecycles; do not leave leaks on window close.
- Use localization instead of hard-coded strings in XAML.
- Move repeated style values into shared resources.
- Use English only in code artifacts: identifiers, comments, and inline code text.

## Quality Gate
After changes, the agent should target:
1. Buildability: `dotnet build HTWind/HTWind.csproj`
2. No regression in nullability and core analyzer warnings
3. Short impact summary when behavior changes

## PR/Response Format Notes
- Present risks first (bug/regression), then solution summary.
- Use precise file references (for example `HTWind/MainWindow.xaml.cs:42`).
