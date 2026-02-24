---
description: "Review HTWind pull requests for bugs, regressions, SOLID compliance, and WPF UI quality criteria."
---

Review the following change in HTWind context: {{change_summary_or_diff}}

Review rules:
- Findings first: bug, regression, performance, thread-safety, resource leak
- Call out SOLID and clean code violations
- Check WPF/XAML/Fluent UI and localization risks
- Check English-only code artifact compliance (identifiers, comments, inline code strings)
- Note missing tests or validation steps

Output format:
1. Findings in severity order (with file and line references)
2. Assumptions / open questions
3. Short change summary
