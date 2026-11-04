# Security review: intentional modernization assessment debt

**Reviewed:** 2026-11-04
**Scope:** The intentional security findings added to the legacy .NET Framework 4.8 application.

This file documents training fixtures, not production-safe code. The legacy app must not be deployed outside a local demo
environment. The findings are designed for the GitHub Copilot upgrade agent to inventory during assessment, schedule during
planning, and remove or replace during execution.

## Findings

| # | Severity | File | Lines | Vulnerability | Confidence |
|---|----------|------|-------|---------------|------------|
| 1 | 🟠 HIGH | `legacy/TaxApp.Web/TaxApp.Web.csproj` | 26 | Vulnerable direct dependency `System.Data.SqlClient` 4.8.5 | 10/10 |
| 2 | 🔴 CRITICAL | `legacy/TaxApp.Web/Helpers/SecurityAssessmentSamples.cs` | 18-27 | Unsafe `BinaryFormatter.Deserialize` of a request-shaped payload | 10/10 |
| 3 | 🟠 HIGH | `legacy/TaxApp.Web/Helpers/SecurityAssessmentSamples.cs` | 29-46 | XXE-capable XML parsing with DTD processing and `XmlUrlResolver` enabled | 10/10 |
| 4 | 🟠 HIGH | `legacy/TaxApp.Web/Helpers/SecurityAssessmentSamples.cs` | 48-60 | SQL injection through string-concatenated request input | 10/10 |
| 5 | 🟡 MEDIUM | `legacy/TaxApp.Web/Helpers/SecurityAssessmentSamples.cs` | 61-64 | Request input returned through unencoded `HtmlString` | 9/10 |
| 6 | 🟡 MEDIUM | `legacy/TaxApp.Web/Helpers/SecurityAssessmentSamples.cs` | 66-71 | Path traversal sink using request input in `Path.Combine` and file read | 9/10 |
| 7 | 🟡 MEDIUM | `legacy/TaxApp.Web/Helpers/SecurityAssessmentSamples.cs` | 73-80 | Weak SHA-1 password hashing | 10/10 |
| 8 | 🟡 MEDIUM | `legacy/TaxApp.Web/Helpers/SecurityAssessmentSamples.cs` | 82-85 | Predictable `Random` token generation | 9/10 |
| 9 | 🟡 MEDIUM | `legacy/TaxApp.Web/Helpers/SecurityAssessmentSamples.cs` | 87-95 | Cookie created with `Secure` and `HttpOnly` disabled | 9/10 |
| 10 | ⚪ LOW | `legacy/TaxApp.Web/Web.config` | 16-17 | Hard-coded, intentionally fake `SecurityDemoPassword` | 9/10 |

Severity describes the underlying pattern if reachable. Current exposure is constrained deliberately:

- `SecurityAssessmentSamples` is internal and every method is private.
- No controller, route, startup hook, test, reflection call or other application code calls the helper.
- The SQL connection uses integrated authentication to a synthetic `localhost` database and no database is provided.
- The credential and cookie values contain no real secret or personal data.
- The vulnerable package is a real direct dependency so `dotnet list package --vulnerable` can detect it; it is not used by the
  production tax workflows.

The local NuGet audit reports `System.Data.SqlClient` 4.8.5 with a **High** advisory:
[GHSA-98g6-xh36-x2p7](https://github.com/advisories/GHSA-98g6-xh36-x2p7).

## Expected modernization remediation

1. **Assess:** list the dependency, every code/config finding, its file, and the recommended remediation.
2. **Plan:** make dependency replacement/removal, sample deletion or safe replacement, and config cleanup explicit tasks.
3. **Execute:** remove the vulnerable package and sample code/config; never add endpoints or callers to make a demo finding pass.
4. **Validate:** run tests and the unchanged HTTP contract with `./go.ps1 -Compare`.

`./scripts/Test-SecurityAssessmentFixtures.ps1` verifies the expected source patterns, absence of callers, private visibility and
the current NuGet audit result. It is a fixture check, not a claim that the VS Code agent UI or its wording is machine-verifiable.
