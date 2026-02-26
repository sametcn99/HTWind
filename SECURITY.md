# Security Policy

## Supported Versions

The following versions of HTWind are currently being supported with security updates.

| Version | Supported          |
| ------- | ------------------ |
| 1.x     | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

We take the security of HTWind seriously. If you believe you've found a security vulnerability in HTWind, please report it to us as follows:

1. **Do not** open a public issue.
2. Send an email to **<sametcn99@gmail.com>** with a detailed description of the vulnerability, steps to reproduce, and potential impact.
3. We will acknowledge your report within 48 hours and work with you to understand and address the issue.

Please note that HTWind allows the execution of PowerShell commands by design via its host bridge API (`window.HTWind.invoke("powershell.exec", ...)`). This feature requires explicit user consent on first launch. Reports related to the *existence* of this feature or its inherent risks (when used as intended) will be closed as "intended behavior," unless you find a way to bypass the consent screen or execute commands without user-initiated widget activity.

### Vulnerability Disclosure Process

1. The reporter sends a private email.
2. The maintainer validates the report and starts working on a fix.
3. Once the fix is ready, a new version is released.
4. The maintainer will then post a security advisory or credit the reporter (if desired) in the release notes.

Thank you for helping keep HTWind safe!
