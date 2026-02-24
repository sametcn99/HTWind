# Contributing to HTWind

Thank you for contributing to HTWind.
This guide explains the expected workflow, quality standards, and review checklist for contributors.

## Table of Contents

- [Scope and Principles](#scope-and-principles)
- [Ways to Contribute](#ways-to-contribute)
- [Development Prerequisites](#development-prerequisites)
- [Repository Setup](#repository-setup)
- [Build and Run](#build-and-run)
- [VS Code Tasks](#vs-code-tasks)
- [Project Structure at a Glance](#project-structure-at-a-glance)
- [Branching and Commit Guidelines](#branching-and-commit-guidelines)
- [Code Quality Guidelines](#code-quality-guidelines)
- [Pull Request Guidelines](#pull-request-guidelines)
- [Pull Request Template](#pull-request-template)
- [Validation Checklist](#validation-checklist)
- [Manual Test Scenarios](#manual-test-scenarios)
- [Widget Contribution Guidelines](#widget-contribution-guidelines)
- [Widget Submission Checklist](#widget-submission-checklist)
- [Localization Contribution Guidelines](#localization-contribution-guidelines)
- [Localization Checklist](#localization-checklist)
- [Security and Safety Notes](#security-and-safety-notes)
- [Packaging Locally](#packaging-locally)
- [Reporting Bugs and Requesting Features](#reporting-bugs-and-requesting-features)
- [Review and Merge Expectations](#review-and-merge-expectations)
- [License and Attribution](#license-and-attribution)

## Scope and Principles

HTWind is a `.NET 10` WPF desktop application that hosts HTML widgets.
Contributions should preserve existing behavior and follow these principles:

- Keep changes focused and minimal.
- Prefer maintainable solutions over quick workarounds.
- Preserve architecture boundaries.
- Avoid introducing breaking changes without a clear migration note.
- Document user-visible behavior changes.

Project-specific expectations:

- Keep business logic out of heavy UI event handlers.
- Prefer interface-based services (`I*`) for new dependencies.
- Keep code and inline comments in English.
- In XAML, avoid hard-coded UI strings and prefer localization resources.

## Ways to Contribute

You can contribute in many ways:

- Bug fixes
- New built-in widgets
- Host API improvements
- UI/UX improvements for WPF screens
- Stability/performance improvements
- Documentation and localization updates
- Maintainability and testability improvements

## Development Prerequisites

- Windows 10/11
- .NET SDK 10.0+
- Git
- Optional: VS Code with C# tooling

## Repository Setup

1. Fork the repository.
2. Clone your fork.
3. Create a feature branch.

```powershell
git clone https://github.com/<your-user>/HTWind.git
cd HTWind
git checkout -b feat/short-description
```

## Build and Run

Run from repository root:

```powershell
dotnet restore HTWind/HTWind.csproj
dotnet build HTWind/HTWind.csproj
dotnet run --project HTWind/HTWind.csproj
```

Optional cleanup:

```powershell
dotnet clean HTWind/HTWind.csproj
```

## VS Code Tasks

The workspace includes tasks for common operations:

- `restore HTWind`
- `build HTWind`
- `run HTWind`
- `clean HTWind`
- `package HTWind installer (local)`
- `package HTWind portable (local)`
- `package HTWind all (local)`

Run tasks from `Terminal` -> `Run Task...`.

## Project Structure at a Glance

Main folders:

- `HTWind/Views`: WPF windows and visual layer
- `HTWind/ViewModels`: presentation logic
- `HTWind/Services`: app, widget, and platform services
- `HTWind/Templates`: built-in HTML widgets
- `HTWind/Resources`: localization resources
- `scripts`: local packaging scripts

Keep changes within the existing responsibility boundaries.

## Branching and Commit Guidelines

Recommended branch naming:

- `feat/<short-description>`
- `fix/<short-description>`
- `docs/<short-description>`
- `refactor/<short-description>`

Commit recommendations:

- Write clear, imperative commit messages.
- Keep commits small and logically focused.
- Avoid mixing refactor-only and behavior-changing edits in one commit.

Examples:

- `fix: prevent widget resize handle from disappearing`
- `docs: expand contributing guide`
- `feat: add built-in drive health widget`

## Code Quality Guidelines

General expectations:

- Favor small, focused methods and classes.
- Avoid magic strings/numbers when practical.
- Use null and argument guards where appropriate.
- Handle file system, registry, and OS-dependent operations defensively.

WPF-specific expectations:

- Keep UI responsive; do not block the UI thread with long-running work.
- Keep user-facing text localizable.
- Preserve accessibility basics (tooltips, keyboard navigation, readable layouts).

Architecture expectations:

- Prefer constructor-injected dependencies.
- Extend behavior through services/interfaces when possible.
- Avoid broad, unrelated refactors in bugfix PRs.

## Pull Request Guidelines

Before opening a PR:

1. Rebase or merge latest upstream changes.
2. Ensure local build succeeds.
3. Review your diff for unrelated changes.
4. Update docs/localization when behavior or text changes.

PR description should include:

- What changed
- Why it changed
- How it was tested
- Known risks or limitations
- Screenshots/GIFs for UI changes

Keep PRs focused. Smaller PRs are safer and faster to review.

## Pull Request Template

You can use this template:

```markdown
## Summary
- What changed

## Motivation
- Why this change is needed

## Testing
- Commands run
- Manual scenarios validated

## Impact
- User-visible impact
- Risk and rollback notes

## Screenshots (if UI change)
- Before/after visuals
```

## Validation Checklist

Use this checklist before opening or updating a PR:

- `dotnet build HTWind/HTWind.csproj` succeeds.
- No obvious nullability or analyzer regressions are introduced.
- New/changed UI text is localized where applicable.
- No regression in tray behavior, startup behavior, or widget persistence.
- Documentation is updated for user-visible changes.

## Manual Test Scenarios

Run relevant manual checks based on your change scope:

- Launch app, add a widget, close app, relaunch, verify state restore.
- Toggle widget `Visible`, `Locked`, and `Pin on top` controls.
- Verify tray icon actions (show/hide/exit).
- Open editor and validate save/live preview behavior.
- Validate localization rendering if text/resource keys changed.
- Validate host bridge behavior if PowerShell execution logic changed.

## Widget Contribution Guidelines

Built-in templates live in `HTWind/Templates`.

When adding or updating a widget:

- Keep widget HTML/CSS/JS self-contained when possible.
- Avoid unnecessary external dependencies.
- Ensure expected state survives app restart.
- Document host bridge usage clearly.

For widgets using host bridge:

- Supported call is `window.HTWind.invoke("powershell.exec", args)`.
- Keep commands intentionally scoped.
- Respect output/timeout boundaries (`maxOutputChars`, `timeoutMs`).
- Avoid risky default scripts.

## Widget Submission Checklist

Before opening a widget-focused PR:

- Widget has a clear name and purpose.
- Markup is readable and reasonably formatted.
- No private/local absolute path dependency exists.
- External APIs/services are documented.
- Error and empty states are handled gracefully.
- Widget behavior is verified after fresh launch.

## Localization Contribution Guidelines

HTWind uses `HTWind/Resources/Strings.resx` and localization services.

To add a language:

1. Create `Strings.<culture>.resx` in `HTWind/Resources`.
2. Copy all keys from `Strings.resx`.
3. Translate values only.
4. Keep placeholders and formatting tokens unchanged.
5. Build and test.

For local verification, temporarily switch startup culture in `HTWind/App.xaml.cs` and run the app.

## Localization Checklist

- Keys are unchanged and aligned with `Strings.resx`.
- Placeholders and interpolation tokens are preserved.
- UI tone and punctuation are consistent.
- Longer translations are checked for overflow/truncation.
- New user-facing strings are added to resources.

## Security and Safety Notes

HTWind supports PowerShell execution through host bridge.
Contributors should avoid lowering the safety posture:

- Do not broaden execution capabilities casually.
- Keep user intent explicit for potentially destructive actions.
- Document behavior changes affecting command execution.

If you discover a security issue, do not publish exploit details in a public issue.
Open a private report to the maintainer first.

Security report should include:

- Affected component
- Reproduction steps
- Potential impact
- Suggested mitigation (if available)

## Packaging Locally

Terminal scripts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-installer.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-portable.ps1
```

Or use VS Code tasks listed above.

## Reporting Bugs and Requesting Features

Use GitHub Issues and include:

- Reproduction steps
- Expected behavior
- Actual behavior
- Environment details (Windows version, .NET SDK, app version)
- Screenshots/log snippets where relevant

Links:

- Issues: <https://github.com/sametcn99/HTWind/issues>
- Discussions: <https://github.com/sametcn99/HTWind/discussions>

## Review and Merge Expectations

- Maintainers may request smaller scope for oversized PRs.
- Requested changes should be addressed with focused follow-up commits.
- If a large rewrite is pushed, summarize it in a PR comment.
- Merge timing depends on risk, reviewer availability, and release timing.

## License and Attribution

By contributing, you agree that your contribution is licensed under the repository license.
If third-party code/assets are included, ensure compatibility and provide attribution when required.

Thank you for helping improve HTWind.
