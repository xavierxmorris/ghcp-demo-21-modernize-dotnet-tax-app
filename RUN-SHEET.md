# Run sheet: modernize the Tax App with GitHub Copilot in VS Code

One page for the presenter. The beats follow the dotnet team's video
[Modernize .NET Apps with GitHub Copilot](https://www.youtube.com/watch?v=DKTcSpJrcGo) (13 min 43 s, published 2026-06-16);
the video timestamps in the first column are for orientation only. Everything is paraphrased; nothing is copied from the video.

**Fictional demo. Illustrative tax figures, not tax advice, not an ATO service. Synthetic data only.**

## Before you present (10 minutes, once)

| Do | Why |
| --- | --- |
| `./go.ps1 -Check` and wait for `CHECK PASS` | Proves both apps, the VS Code tasks, the workspace script and the checks work on your machine |
| Install **GitHub Copilot upgrade** (`code --install-extension ms-dotnettools.upgrade-agent`) and sign in to GitHub in VS Code | The live agent. `-Check` only warns if it is missing |
| Disable the older **GitHub Copilot modernization for .NET** extension | It is deprecated and adds a second `@modernize-dotnet` participant that muddies the demo |
| In VS Code open Copilot Chat, send `@upgrade` and confirm it answers | Documented verification step on Microsoft Learn |
| Decide your approvals mode (below) | The video bypassed approvals to avoid repeated prompts |
| Do one full rehearsal run | The first start can take a while, and the agent's choices vary run to run |

### Which agent do I pick?

The video shows an agent called **Modernize .NET** from the extension **GitHub Copilot modernization for .NET**
(`ms-dotnettools.vscode-dotnet-modernize`). That extension's own README now says it is **deprecated and replaced by GitHub Copilot
upgrade** (`ms-dotnettools.upgrade-agent`), and its agent file says the same for the `@modernize-dotnet` agent (read from the
installed extension, 2026-10-26). Microsoft Learn describes the replacement workflow: start the agent with `@upgrade` or the
**Upgrade** entry in the Agent picker, answer the pre-initialisation questions, then three stages (assessment, planning, execution)
that write Markdown files under `.github/upgrades/{scenarioId}`.

So: **use `Upgrade` today**. If your VS Code still shows **Modernize .NET**, the beats below are the same. The separate **GitHub
Copilot modernization** extension (`vscjava.migrate-java-to-azure`, agent `Modernize`) is for Azure migration tasks and is not needed
for this version upgrade.

### Approvals

VS Code has three permission levels in the chat input: **Default Approvals** (you approve each action), **Bypass Approvals**
(tool calls are auto-approved) and **Autopilot** (also answers the agent's questions for you); see
[Your first agent session](https://code.visualstudio.com/learn/foundations/approvals-autonomy-and-context-budget). The video chose
bypass. Do that **only in the disposable workspace**, and say out loud why it is safe there (a throwaway git repo with only the app).

## The beats

Target times are rehearsal targets, not measurements. Machine steps are marked **[script]**; the rest are **[UI]**.

| Video | Beat | You say | You do | You should see |
| --- | --- | --- | --- | --- |
| 0:00 | **Why modernize** (1 min) | Modern .NET brings performance, security, current libraries and cloud-native hosting. The hard part is not the version number, it is project files, dependencies and API replacements | Nothing | Title slide or this page |
| 0:58 | **What the agent does** (30 s) | An agentic experience: it assesses, plans, executes, validates and helps fix issues, with Markdown files you can read and edit | Nothing | |
| 1:22 | **Meet the app** (2 min) **[script]** | This is a real-shaped .NET Framework 4.8 MVC app: estimator, deductions, return summary, GST | `./go.ps1 -Run Legacy`, then click each page | Browser at `localhost:8081`; footer says **Running on .NET Framework 4.8** |
| | **Open the workspace** **[script]** | The agent changes files and makes commits, so we work in a throwaway copy | `./go.ps1 -Reset` | VS Code opens on a clean git repo; build and tests already proven; prompt on your clipboard |
| 1:44 | **Pick the agent and a model** **[UI]** | The extension is installed; I pick the agent and the model, then I am explicit about what I want | Chat view, Agent picker, **Upgrade**, choose a model, paste the prompt from [PROMPTS.md](PROMPTS.md#1-the-upgrade-prompt) | The agent starts its workflow |
| 2:05 | **Approvals and confirmations** **[UI]** | I do not want to approve every command in a throwaway repo. The agent asks a few questions up front | Choose your approvals mode; answer: **.NET 10**, Automatic (or Guided), branch name, **commit after each task** | The agent checks out a new branch |
| 3:11 | **Assessment** **[UI]** | The agent reads projects, dependencies and APIs. This is what needs to change, before anything changes | Open `assessment.md` under `.github/upgrades/` and preview it | Inventory, blockers and recommendations. See [docs/LEGACY-DEBT.md](docs/LEGACY-DEBT.md) for what a good assessment mentions (keep it off screen) |
| 3:58 | **Skills** **[UI]** | Your organisation's standards live in `.github/skills`. Drop a skill in and the agent applies it during the run | Show `.github/skills/tax-money-rules/SKILL.md` | Money is `decimal`, explicit rounding, contract unchanged. *Rehearse:* watch whether the agent mentions it |
| 4:20 | **Upgrade options** **[UI]** | Strategy and approach are decisions, not defaults | All-at-once; change **side-by-side** to **In-place rewrite**; confirm | The agent saves the options |
| 5:11 | **Plan and tasks** **[UI]** | The assessment says what; the plan says how. Both are Markdown you can edit and commit | Open `plan.md` then `tasks.md`; optionally edit a line | A short task list. The video's app had four tasks (verify, convert to SDK style, upgrade with package and API fixes, final validation). The Tax App is already SDK style, so expect fewer |
| 6:54 | **Watch it work** **[UI]** | You keep full visibility and full control | Dashboard or progress view if your build shows one (the video shows one); otherwise `tasks.md` | Commits per task. *Rehearse:* whether the dashboard opens in your VS Code build |
| 10:58 | **Keep or undo** **[UI]** | Every change is reviewable in the editor | Review the diff; **Keep** | |
| 11:21 | **Done: but did it keep its promises?** **[script]** | The business functionality should be intact. Do not take my word for it | `./go.ps1 -Compare` | Build OK, tests pass, `net10.0`, **HTTP contract PASS**. Open the new `.csproj` too |
| | **Optional: what drift looks like** **[script]** | A framework upgrade can silently change rounding or JSON casing. Here is what the checks say when it does | `./go.ps1 -DriftDemo` | Red checks naming the exact difference (for example `expected 0.05 got '0.04'`) |
| 12:13 | **Everywhere, and at scale** (1 min) | The video says the same experience is in Visual Studio, VS Code, GitHub Copilot CLI, the Copilot cloud agent and the Modernize CLI. The Modernize CLI does batch assessment, planning and execution across a portfolio | Show the commands in [PROMPTS.md](PROMPTS.md#7-portfolio-scale-the-modernize-cli-video-last-beat) | Not run in this repo |
| 12:54 | **Resources** | Docs, the agent's GitHub repo for feedback, the VS Code extension. The team also announced Web Forms to Blazor support | Show the links in [README.md](README.md#sources) | |

If the agent stalls or the network is slow: `./go.ps1 -Run Modern` shows the prebaked .NET 10 reference. **It is not agent output.**
Say so.

## Rehearse these (not machine-verified)

These need a human in VS Code, so the repository cannot prove them:

- The agent picker shows **Upgrade** (or **Modernize .NET**) and the agent completes the run on this app.
- The confirmation questions and options look like the table in [PROMPTS.md](PROMPTS.md#2-answers-to-the-confirmation-questions).
- Whether the agent picks up `.github/skills/tax-money-rules` on its own. The prompt names it explicitly as a safety net.
- Whether a dashboard view opens in your VS Code build.
- The **Legacy Tax App (... experimental)** debug configuration: F5 should start the legacy site under the debugger; breakpoints in
  the site's AppDomain are untested.
- How long your run takes. No time-saved figure is published here; record your own.

## Recovery

| Problem | Do |
| --- | --- |
| Port 8081 or 5081 is busy | `./go.ps1 -Run Legacy -Port 9081` (or `Modern -Port 6081`) |
| The workspace is in a bad state | `./go.ps1 -Reset` recreates it (it refuses to delete a folder it did not create) |
| The agent changed a URL or form field | `./go.ps1 -Compare` names the failing check; ask the agent to restore the public contract in `.github/skills/tax-money-rules` |
| You want the baseline for the workspace | `./go.ps1 -Compare` before the run: `BASELINE GREEN` |
