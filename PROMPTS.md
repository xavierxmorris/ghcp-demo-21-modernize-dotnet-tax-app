# Prompts for the live run

Copy and paste these into **GitHub Copilot Chat** in the disposable workspace (`./go.ps1 -Reset` creates it).
The first prompt is also copied to your clipboard by `-Reset`.

> The video shows the **Modernize .NET** agent from the older "GitHub Copilot modernization for .NET" extension. Microsoft has since
> **deprecated that extension in favour of GitHub Copilot upgrade** (`ms-dotnettools.upgrade-agent`, agent `Upgrade`, chat
> participant `@upgrade`). The workflow is the same shape (assess, plan, tasks). Use whichever agent name your VS Code shows.
> Details and sources: [RUN-SHEET.md](RUN-SHEET.md#which-agent-do-i-pick).

## 1. The upgrade prompt

Pick the agent first: open the Copilot Chat view, then either type `@upgrade` or choose **Upgrade** in the agent picker. Choose a
model you like. Then paste:

<!-- prompt:upgrade -->
```text
Upgrade this solution from .NET Framework 4.8 to .NET 10 (LTS). Keep the behaviour exactly the same: every URL, form field name and JSON property name must stay the same, money must stay decimal with the existing rounding, and the existing tests must still pass on .NET 10. Follow .github/skills/tax-money-rules. Work on a new branch and commit after each task.
```

Being this explicit is the point: the agent plans from your words, and a Tax App has things that must not drift.

## 2. Answers to the confirmation questions

The agent collects these before it starts (documented: target framework, Git branching strategy, workflow mode):

| Question | Suggested answer | Why |
| --- | --- | --- |
| Target framework | **.NET 10** | Current LTS, matches the prompt |
| Workflow mode | **Automatic** to match the video, or **Guided** to pause at each stage | Guided is easier to narrate; Automatic finishes sooner |
| Branch | `upgrade-to-net10` (any name) | Keeps `main` untouched |
| Commit strategy | **After each task** | One reviewable commit per task |
| Upgrade strategy | **All-at-once** | One web project and one test project |
| Project approach | **In-place rewrite** (the video changed it from side-by-side) | Keeps the same folders so the tests and the contract check still find everything |

## 3. Steering words (documented on Microsoft Learn)

| When | Say |
| --- | --- |
| The assessment looks right | `proceed to the planning stage` |
| The plan looks right | `move to the execution stage` |
| Ready to run the tasks | `start the upgrade` |

Open `assessment.md`, `plan.md` and `tasks.md` under `.github/upgrades/` (the scenario folder name varies) and edit them if you
want; they are plain Markdown.

## 4. Optional: a custom upgrade instruction (the video's "skills" beat)

The workspace already contains a skill at `.github/skills/tax-money-rules/SKILL.md`. To show a one-transformation instruction
as well, use the example from Microsoft Learn:

```text
I want to generate a custom upgrade instruction
```

then:

```text
I want to replace Newtonsoft with System.Text.Json
```

The Tax App uses Newtonsoft.Json in exactly one place (the deductions file). The instruction must keep the file format
identical; check `DeductionRepositoryTests` and the contract run afterwards.

## 5. Prove the business functionality is intact

Ask the agent:

```text
Run the tests and show me the real result. Then list anything that changed behaviour.
```

Then verify it yourself, outside the agent:

```powershell
./go.ps1 -Compare
```

That builds and tests the upgraded workspace, starts the upgraded app and runs the same HTTP contract that the legacy app and
the prebaked reference pass. A red check means behaviour drifted.

## 6. If the agent stalls or the network is slow

Show the prebaked reference instead. It is **not** agent output; say so out loud.

```powershell
./go.ps1 -Run Modern
```

## 7. Portfolio scale: the Modernize CLI (video, last beat)

From the Microsoft Learn quickstart (not run in this repository):

```powershell
winget install GitHub.Copilot.modernization.agent
modernize --version
gh auth login
modernize
```

`modernize` opens a menu with **Assess**, **Plan**, **Execute** and an **Upgrade** quick start. Assessments are written to
`.github\modernize\assessment\` and plans to `.github/modernize/{plan-name}/plan.md`.
