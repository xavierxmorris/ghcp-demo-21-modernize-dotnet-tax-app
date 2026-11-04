# Tax App (disposable demo workspace)

This is a fictional ASP.NET MVC 5 application on .NET Framework 4.8 that estimates Australian resident income tax, tracks
deductions, summarises a return and calculates GST. It is a demo, not tax advice. All figures are illustrative.

- Solution: `TaxApp.sln` (`TaxApp.Web` MVC app, `TaxApp.Tests` xUnit tests).
- Build and test: `dotnet build TaxApp.sln` then `dotnet test TaxApp.sln`.
- Money rules and the public contract are in `.github/skills/tax-money-rules/SKILL.md`. Follow them.
- The intentional findings in `TaxApp.Web/Helpers/SecurityAssessmentSamples.cs`, its direct vulnerable package and the fake
  `SecurityDemoPassword` are legacy security debt to inventory, plan and remediate. Never add a caller, route or real secret.
- Golden master: `TaxApp.Tests/Golden/tax-vectors.json`. Never change expected values to make a test pass.
- Do not use real personal data. Use obviously fake data only.
