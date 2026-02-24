---
applyTo: "HTWind/**/*.xaml"
description: "Ensures consistency, accessibility, and maintainability for WPF XAML and Fluent UI changes."
---

## XAML and Fluent UI Rules
- Do not add hard-coded text; use localization extension (`loc:Loc`).
- Prefer dynamic resources or shared styles for color/font/spacing values.
- Prefer Fluent tokens such as `TextFillColorSecondaryBrush`; minimize direct hex colors.
- Avoid deeply nested `StackPanel` layouts; use `Grid` for scalable layouts.
- Ensure min-size and wrapping/trimming settings remain stable across window sizes and DPI.
- Use English only in code artifacts and inline code text (identifiers, comments, and control names).

## Accessibility
- For icon-only buttons, provide meaningful `ToolTip` or `AutomationProperties.Name`.
- Do not introduce focus/click behaviors that break keyboard usage.
- Avoid low-contrast combinations.

## Maintenance and Readability
- Move long `DataTemplate` blocks to `ResourceDictionary` where needed.
- Standardize repeated card/row structures using styles/templates.
- Keep event handler naming consistent (`<Control>_<Action>`).

## Fluent UI Experience
- Keep `Appearance` and `SymbolIcon` usage semantic (`Danger` only for destructive actions).
- Use consistent typography hierarchy (`FontSize`, `FontWeight`).
