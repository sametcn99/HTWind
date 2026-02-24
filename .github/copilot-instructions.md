# HTWind Copilot Instructions

This repository, `HTWind`, is a .NET 10 WPF desktop application. Copilot is expected to propose and apply changes that fit the current architecture and remain testable and maintainable.

## Project Context
- Technology: `.NET 10`, `WPF`, `WPF-UI`, `WebView2`.
- Target framework: `net10.0-windows10.0.19041.0`.
- Core layers: UI (`*.xaml`, `*.xaml.cs`), service interfaces and implementations (`HTWind/Services/*`).
- Localization is active: `HTWind/Localization/*`, `HTWind/Resources/Strings.resx`.

## Code Generation Rules
- Treat `SOLID` principles as default:
  - Single responsibility: do not grow business logic inside UI event handlers.
  - Abstract dependencies: prefer interfaces (`I*`) whenever possible.
  - Open for extension: add new behavior without unnecessary modification of existing classes.
- `Clean Code`:
  - Keep methods small and purpose-driven.
  - Use clear naming; avoid vague abbreviations and one-letter variables.
  - Replace magic numbers/strings with constants or configuration.
  - Use broad `catch` only when intentional and justified; note loggable conditions.
- Null safety:
  - Follow nullable reference type rules.
  - Prefer `ArgumentNullException.ThrowIfNull(...)` and guard clauses.
- Language policy:
  - Use English only in code artifacts: identifiers, comments, user-facing code strings, and inline documentation.

## .NET Desktop and WPF Principles
- UI thread safety: route UI updates through Dispatcher/UI thread.
- `async/await`:
  - Move long-running work off the UI thread.
  - Use `async void` only in event handlers.
- Resource management:
  - Manage event subscriptions and disposables to avoid leaks.
- Architecture:
  - For new screens/features, move toward MVVM and avoid heavy business logic in code-behind.
  - Use constructor injection for service dependencies.

## XAML and Fluent UI Expectations
- Centralize repeated style/color/spacing values via `ResourceDictionary`.
- Use localization (`LocExtension`) instead of hard-coded UI text.
- Accessibility:
  - Keep keyboard accessibility, tooltips, and meaningful control names.
  - Prefer Fluent UI tokens for contrast and readability.
- Layout:
  - Prefer `Grid`/`DockPanel` for responsive behavior and avoid unnecessary nested `StackPanel` structures.
  - Use appropriate `MinWidth/MinHeight` and `TextTrimming`.

## While Making Changes
- Produce minimal, targeted diffs; avoid unrelated refactors.
- Preserve existing naming and folder structure.
- Build command: `dotnet build HTWind/HTWind.csproj`.
- If behavior changes, include a short impact note.

## Copilot Response Style
- Provide suggestions with project file paths (`HTWind/Services/...`, `HTWind/MainWindow.xaml`, etc.).
- For larger changes, provide a short plan first, then implementation details.
- Offer alternatives when useful, but default to the lowest-risk solution.
