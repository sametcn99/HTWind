---
applyTo: "HTWind/**/*.cs,HTWind/**/*.csproj,HTWind.slnx"
description: "Applies SOLID, clean code, and desktop app best practices for .NET 10 WPF code changes."
---

## Scope
This instruction applies to C# and project file changes.

## Mandatory Rules
- Preserve backward compatibility when changing public APIs, or document the impact explicitly.
- Do not perform long-running/blocking work inside event handlers.
- Use `async void` only for event handlers; return `Task` otherwise.
- Marshal UI-bound operations to the Dispatcher when called off the UI thread.
- Use `ArgumentNullException.ThrowIfNull(...)` and guard clauses.
- Prevent resource leaks where `IDisposable` and event lifecycles are involved.
- Use English only in code artifacts: identifiers, comments, XML docs, and user-facing code strings.

## SOLID Application
- SRP: Keep code-behind focused on UI orchestration; place business logic in services.
- OCP: Add new widget behavior without large, unnecessary edits to existing classes.
- LSP/ISP: Keep interfaces focused and small; avoid unnecessary members.
- DIP: Prefer interface-based dependencies over concrete types.

## Clean Code
- Keep methods short and single-purpose.
- Use meaningful names; avoid ambiguous abbreviations.
- Replace magic values with constants/enums.
- Use broad `catch` only for intentional fallback; add a comment or logging note when possible.

## Desktop App Specific
- Handle registry, file system, and Win32 access with error tolerance.
- Use safe parse/fallback behavior for corrupted persisted state.
- Avoid UX regressions in tray lifecycle and window-close behavior.

## Validation
When possible, run `dotnet build HTWind/HTWind.csproj` after changes and report the result.
