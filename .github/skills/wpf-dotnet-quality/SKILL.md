---
name: wpf-dotnet-quality
description: "A .NET desktop, WPF, SOLID, and clean code focused change/review workflow for HTWind."
---

# WPF .NET Quality Skill

## When to use
- When C# or XAML changes are requested
- When a refactor is requested
- When evaluating regression risk after a bug fix

## Workflow
1. Identify affected UI, service, and model classes.
2. Check whether business logic has leaked into UI code-behind.
3. Run SOLID checks:
   - Is class responsibility too broad?
   - Are interface-based dependencies preserved?
4. Check desktop risks:
   - UI thread blocking
   - Event/resource leaks
   - File/registry error tolerance
5. Check XAML:
   - Localization, dynamic resources, accessibility
6. Run build when possible and report results.

## Output Standard
- Critical findings first
- Then implemented changes
- Then residual risk and test notes
