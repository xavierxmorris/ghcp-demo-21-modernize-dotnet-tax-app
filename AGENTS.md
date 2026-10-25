# AGENTS.md

Instructions for any coding agent working in this repository: Copilot cloud agent, Copilot CLI, Copilot in the IDE, or another
agent that reads `AGENTS.md`. Repository conventions are in [`.github/copilot-instructions.md`](.github/copilot-instructions.md).

> Fictional demo. Illustrative tax figures, not tax advice, not an ATO service. Synthetic data only.

## What this repository is

A runnable rebuild of the dotnet team's "Modernize .NET Apps with GitHub Copilot" demo. `legacy/` is a .NET Framework 4.8
ASP.NET MVC 5 **Tax App**. `modern/` is a hand-built .NET 10 reference. The same golden master and HTTP contract prove both behave
the same. See [README.md](README.md).

## Setup

Windows, .NET SDK 10 (pinned by `global.json`), PowerShell 7, Git. Nothing to install beyond what `dotnet` restores.

## Verification loop: run this before you claim anything works

```powershell
./go.ps1 -Check          # everything, about 4 to 6 minutes; the gate
dotnet test legacy/TaxApp.sln        # .NET Framework 4.8 tests (Windows only)
dotnet test modern/TaxApp.sln        # .NET 10 tests
python scripts/golden/new_tax_vectors.py --check      # golden master still matches its reference
```

For one site at a time: `./go.ps1 -Run Legacy` or `./go.ps1 -Run Modern`, then
`./scripts/Invoke-TaxContract.ps1 -BaseUrl http://localhost:8081 -Label legacy`.

`./go.ps1 -Check` is the only thing that proves the VS Code configuration works: it executes every task in `.vscode/tasks.json`
as written. Keep tasks as `"type": "process"` with `command` and `args` so that stays true.

## Hard rules

1. **Never hand-edit `legacy/TaxApp.Tests/Golden/tax-vectors.json` or weaken what reads it.** It is generated from an independent
   reference calculation. If a vector fails, the code is wrong. Regenerate only by changing the reference script, and say why.
2. **Never make a check pass by weakening it.** Do not loosen the HTTP contract, delete a test, widen a tolerance or skip a vector.
   A red check is information.
3. **`legacy/` is deliberately legacy.** Do not modernize it, add nullable annotations, switch it to `System.Text.Json`, or use
   syntax newer than C# 7.3 in `.cs` files and C# 5 in `.cshtml` files (the .NET Framework view compiler is C# 5).
   Changing `legacy/` changes what the demo shows the agent. If you must, re-run `./go.ps1 -Check`.
4. **`modern/` must keep passing the same contract as `legacy/`.** If you change observable behaviour in one, change both and the
   vectors deliberately, and explain it in your summary.
5. **Never present `modern/` as the upgrade agent's output.** It is a hand-built reference. Keep the README "Honesty notes" true.
6. **Money is `decimal`; rounding is `Math.Round(value, 2, MidpointRounding.AwayFromZero)`** on each component. Do not use `double`.
7. **Fictional data only.** No real personal information, no credentials, no secrets. The tax is illustrative.
8. **Do not add dependencies** without saying what they replace. Scripts target PowerShell 7.
9. **Do not commit build output or runtime state** (`bin/`, `obj/`, `App_Data/deductions.json`, `App_Data/logs/`).
10. **Do not claim what you did not run.** The Copilot upgrade agent, the dashboard and debugger attach are UI steps nobody here has machine-verified.

## Reporting

When you finish, state:

- which commands you ran and the real result (paste the `CHECK PASS` / `CHECK FAIL` line);
- whether any vector or contract check changed, and whether that was a regression or a deliberate, explained change;
- anything you could not verify.
