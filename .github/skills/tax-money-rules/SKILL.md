---
name: tax-money-rules
description: Rules for money, rounding, tax tables and the public contract of the Tax App. Use when upgrading, refactoring or editing tax calculation, GST, deductions, JSON output, forms or tests in this solution.
---

# Tax App: money and contract rules

Apply these whenever you touch calculation code, JSON, forms, views or tests in this solution, including during a
framework upgrade. They exist because the app computes money.

1. **Money is `decimal`.** Never `double` or `float`, not in services, models, tests or JavaScript-facing code.
2. **Rounding is explicit.** Round each tax component (income tax, Medicare levy, GST) with
   `Math.Round(value, 2, MidpointRounding.AwayFromZero)`, then add the rounded components. Do not switch to the default
   (round-to-even) behaviour and do not round only the total.
3. **Tax tables are data that mirror the published ATO scale.** A framework upgrade must not change a rate, a threshold or a
   base amount.
4. **Keep the public contract unchanged.** URLs and action names, form field names (`TaxYear`, `Income`, `Deductions`,
   `Amount`, `Mode`, `Withheld`, `Description`, `Category`), the `data-testid` and `data-value` attributes in views, and the
   PascalCase JSON property names returned by `/Tax/EstimateJson`.
5. **Keep the deductions file format.** `App_Data/deductions.json` with PascalCase property names and enum values written as
   strings, so files written before the upgrade still load. Keep it **outside** `wwwroot`: anything under `wwwroot` is served to
   the public, and this file holds financial data.
6. **`Golden/tax-vectors.json` is the golden master.** Never edit an expected value to make a test pass. If a vector fails, the
   code is wrong.
7. **Verify before you say it works.** Run `dotnet test` and report the real result.
