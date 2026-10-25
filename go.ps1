#requires -Version 7.0
<#
.SYNOPSIS
    One-command runner for the "Modernize a .NET Framework app with GitHub Copilot" demo (the Tax App).

.DESCRIPTION
    The demo: a legacy ASP.NET MVC 5 "Tax App" on .NET Framework 4.8 is upgraded to .NET 10 by the GitHub Copilot upgrade agent
    in VS Code, and you prove the business functionality is intact. This script runs every machine step. The agent itself is a
    UI step in VS Code (see RUN-SHEET.md).

    Windows is required (the legacy half is .NET Framework). The modern app runs anywhere .NET 10 runs.

.PARAMETER Check
    Verify everything end to end and set the exit code (this is the default). Runs the VS Code tasks exactly as written,
    both launch configurations, the golden master, the HTTP contract against the legacy and modern apps, the disposable
    workspace and the compare step, a seeded-regression self-test and the documentation links.

.PARAMETER Run
    Start a site and open it in the browser: Legacy (http://localhost:8081, .NET Framework 4.8) or Modern
    (http://localhost:5081, .NET 10). Press Enter to stop.

.PARAMETER Reset
    Create the disposable demo workspace (a clean git repo holding only the legacy app), put the upgrade prompt on the
    clipboard and open VS Code on it.

.PARAMETER Compare
    After the agent has run: build and test the workspace, start the upgraded app and run the HTTP contract.
    Use -Target to point at another .sln, .slnx or .csproj, for example modern\TaxApp.sln.

.PARAMETER DriftDemo
    Seed two realistic regressions (round-to-even, camelCase JSON) into a scratch copy of the modern app and show the checks
    catch them.

.PARAMETER Manual
    Print the click path for the live run and exit.

.PARAMETER Live
    A guided run of the whole demo with a pause after each beat.

.PARAMETER NoBrowser
    Do not open browser windows.

.PARAMETER NoOpen
    Do not open VS Code (with -Reset or -Live).

.PARAMETER NoClipboard
    Do not put the upgrade prompt on the clipboard (with -Reset or -Live).

.PARAMETER Evidence
    With -Check: write docs/evidence/verification.md.

.EXAMPLE
    ./go.ps1                 # verify everything
    ./go.ps1 -Run Legacy     # the "before"
    ./go.ps1 -Reset          # create the workspace and open VS Code
    ./go.ps1 -Compare        # after the agent has finished
#>
[CmdletBinding()]
param(
    [switch]$Check,
    [ValidateSet('Legacy', 'Modern')][string]$Run,
    [switch]$Reset,
    [switch]$Compare,
    [switch]$DriftDemo,
    [switch]$Manual,
    [switch]$Live,
    [switch]$NoBrowser,
    [switch]$NoOpen,
    [switch]$NoClipboard,
    [switch]$Evidence,
    [int]$Port = 0,
    [string]$WorkspacePath = (Join-Path $HOME 'tax-app-demo-workspace'),
    [string]$Target
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root
. (Join-Path $root 'scripts/lib/Site.ps1')

function Step([string]$Text) { Write-Host "`n=== $Text" -ForegroundColor Cyan }
function Ok([string]$Text) { Write-Host "  OK   $Text" -ForegroundColor Green }
function Bad([string]$Text) { Write-Host "  !!   $Text" -ForegroundColor Red }
function Warn([string]$Text) { Write-Host "  warn $Text" -ForegroundColor Yellow }
function Note([string]$Text) { Write-Host "       $Text" -ForegroundColor DarkGray }

function Open-Url([string]$Url) {
    if ($NoBrowser) { Note "open $Url in your browser"; return }
    try { Start-Process $Url } catch { Note "open $Url in your browser" }
}

function Invoke-Script([string]$Name, [string[]]$Arguments = @()) {
    & pwsh -NoProfile -File (Join-Path $root "scripts/$Name") @Arguments | Out-Host
    return $LASTEXITCODE
}

# ------------------------------------------------------------------ -Manual ---
if ($Manual) {
    Write-Host @'
Manual walkthrough (the full version, with what to say, is RUN-SHEET.md)
------------------------------------------------------------------------
 0. ./go.ps1 -Check                  # everything is green before you present
 1. ./go.ps1 -Run Legacy             # the BEFORE: http://localhost:8081, "Running on .NET Framework 4.8"
 2. ./go.ps1 -Reset                  # disposable workspace, prompt on the clipboard, VS Code opens
 3. VS Code > Copilot Chat > Agent picker > Upgrade (or type @upgrade) > pick a model > paste the prompt
 4. Confirmations: .NET 10, Automatic or Guided, branch upgrade-to-net10, commit after each task
 5. Assessment  -> open assessment.md   (under .github/upgrades/<scenario>)
 6. Options     -> All-at-once, In-place rewrite
 7. Plan, tasks -> open plan.md and tasks.md; edit them if you like; the tasks run
 8. Keep / Undo the changes in the editor when asked
 9. ./go.ps1 -Compare                # build, tests and the HTTP contract against the upgraded app
10. ./go.ps1 -Run Modern             # the AFTER (the prebaked reference, if you want to show it)
11. ./go.ps1 -DriftDemo              # optional: what the checks do when behaviour drifts

If the agent stalls: ./go.ps1 -Run Modern shows the prebaked reference. It is NOT agent output; say so.
'@
    return
}

# ------------------------------------------------------------------- -Reset ---
if ($Reset) {
    $arguments = @('-Path', $WorkspacePath)
    if ($NoOpen) { $arguments += '-NoOpen' }
    if ($NoClipboard) { $arguments += '-NoClipboard' }
    exit (Invoke-Script 'New-DemoWorkspace.ps1' $arguments)
}

# ----------------------------------------------------------------- -Compare ---
if ($Compare) {
    $arguments = @('-WorkspacePath', $WorkspacePath)
    if ($Target) { $arguments += @('-Target', $Target) }
    exit (Invoke-Script 'Compare-Workspace.ps1' $arguments)
}

# --------------------------------------------------------------- -DriftDemo ---
if ($DriftDemo) { exit (Invoke-Script 'Invoke-DriftDemo.ps1') }

# --------------------------------------------------------------------- -Run ---
function Invoke-Run([string]$Kind) {
    Step "Building the $Kind app"
    if ($Kind -eq 'Legacy') {
        dotnet build legacy/TaxApp.sln -nologo -v:minimal | Select-Object -Last 3 | ForEach-Object { Note $_ }
        if ($LASTEXITCODE -ne 0) { throw 'legacy build failed' }
        dotnet build tools/LegacyHost/LegacyHost.csproj -nologo -v:minimal | Select-Object -Last 3 | ForEach-Object { Note $_ }
        if ($LASTEXITCODE -ne 0) { throw 'legacy host build failed' }
        $p = if ($Port) { $Port } else { 8081 }
        $site = Start-LegacySite -Port $p
    }
    else {
        dotnet build modern/TaxApp.sln -nologo -v:minimal | Select-Object -Last 3 | ForEach-Object { Note $_ }
        if ($LASTEXITCODE -ne 0) { throw 'modern build failed' }
        $p = if ($Port) { $Port } else { 5081 }
        $site = Start-ModernSite -Port $p
    }
    try {
        Step "Starting the $Kind app on $($site.Url)"
        if (-not (Wait-HttpReady -Url $site.Url -TimeoutSec 120 -Process $site.Process)) {
            Bad "the app did not start. See $($site.Err) and $($site.Out)"
            Get-Content -LiteralPath $site.Err -Tail 10 -ErrorAction SilentlyContinue | ForEach-Object { Note $_ }
            Get-Content -LiteralPath $site.Out -Tail 10 -ErrorAction SilentlyContinue | ForEach-Object { Note $_ }
            throw "$Kind app did not start (port $p busy?)"
        }
        Ok "$Kind Tax App is up: $($site.Url)"
        Open-Url $site.Url
        Read-Host 'Press Enter to stop it' | Out-Null
    }
    finally { Stop-Site $site }
}

if ($Run) { Invoke-Run $Run; return }

# -------------------------------------------------------------------- -Live ---
if ($Live) {
    function Pause-Beat([string]$Text) { Write-Host "`n>>> $Text" -ForegroundColor Magenta; Read-Host 'Press Enter to continue' | Out-Null }
    Step 'Beat 1: the legacy Tax App (ASP.NET MVC 5, .NET Framework 4.8)'
    Note 'Open the footer: "Running on .NET Framework 4.8". Try the estimator, deductions, return summary and GST pages.'
    dotnet build legacy/TaxApp.sln -nologo -v:minimal | Out-Null
    dotnet build tools/LegacyHost/LegacyHost.csproj -nologo -v:minimal | Out-Null
    $legacy = Start-LegacySite -Port 8081
    try {
        if (Wait-HttpReady -Url $legacy.Url -Process $legacy.Process) { Ok "legacy site: $($legacy.Url)"; Open-Url $legacy.Url }
        else { Bad 'the legacy site did not start' }
        Pause-Beat 'Next: create the disposable workspace and open VS Code.'
    }
    finally { Stop-Site $legacy }

    Step 'Beat 2: the disposable workspace'
    $arguments = @('-Path', $WorkspacePath); if ($NoOpen) { $arguments += '-NoOpen' }; if ($NoClipboard) { $arguments += '-NoClipboard' }
    $null = Invoke-Script 'New-DemoWorkspace.ps1' $arguments
    Pause-Beat 'In VS Code: pick the Upgrade agent, paste the prompt, follow RUN-SHEET.md. Press Enter when the agent has finished.'

    Step 'Beat 3: is the business functionality intact?'
    $arguments = @('-WorkspacePath', $WorkspacePath)
    if ($Target) { $arguments += @('-Target', $Target) }
    $null = Invoke-Script 'Compare-Workspace.ps1' $arguments
    Pause-Beat 'Optional: show the prebaked reference (not agent output).'

    Step 'Beat 4: the prebaked .NET 10 reference'
    Invoke-Run 'Modern'
    return
}

# ------------------------------------------------------------------- -Check ---
$script:rows = [System.Collections.Generic.List[object]]::new()
$script:failed = 0

function Measure-Step([string]$Name, [scriptblock]$Body) {
    Step $Name
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $status = 'pass'; $detail = ''
    try {
        $result = & $Body
        if ($result -is [hashtable]) { $status = $result.Status; $detail = $result.Detail }
    }
    catch { $status = 'FAIL'; $detail = $_.Exception.Message; Bad $detail }
    if ($status -eq 'FAIL') { $script:failed++ }
    $script:rows.Add([pscustomobject]@{ Step = $Name; Status = $status; Seconds = [int]$watch.Elapsed.TotalSeconds; Detail = $detail })
}

function Get-TestLine([string]$LogName) {
    $log = Join-Path ([System.IO.Path]::GetTempPath()) "taxapp-demo/$LogName.log"
    if (-not (Test-Path $log)) { return '' }
    $line = Select-String -LiteralPath $log -Pattern 'Passed!|Failed!' | Select-Object -Last 1
    if ($line) { ($line.Line -replace '\s+', ' ').Trim() } else { '' }
}

function Invoke-ContractFor([string]$Label, $Site) {
    if (-not (Wait-HttpReady -Url $Site.Url -TimeoutSec 120 -Process $Site.Process)) {
        Get-Content -LiteralPath $Site.Err -Tail 8 -ErrorAction SilentlyContinue | ForEach-Object { Note $_ }
        throw "$Label did not start"
    }
    $report = Join-Path ([System.IO.Path]::GetTempPath()) "taxapp-demo/contract-$Label.json"
    $exit = Invoke-Contract -BaseUrl $Site.Url -Label $Label -ReportPath $report
    $summary = if (Test-Path $report) { $r = Get-Content $report -Raw | ConvertFrom-Json; "$($r.passed)/$($r.total) checks; $($r.runtime)" } else { '' }
    if ($exit -ne 0) { throw "HTTP contract failed for $Label ($summary)" }
    Ok "HTTP contract passes for $Label ($summary)"
    @{ Status = 'pass'; Detail = $summary }
}

if (-not ($Check -or $Run -or $Reset -or $Compare -or $DriftDemo -or $Manual -or $Live)) { $Check = $true }

$checkWatch = [System.Diagnostics.Stopwatch]::StartNew()
Write-Host 'Tax App demo: verifying everything (about 4 to 6 minutes)' -ForegroundColor Cyan

Measure-Step '1. Prerequisites' {
    if (-not $IsWindows) { throw 'Windows is required: the legacy app is .NET Framework 4.8. (The modern app runs anywhere: dotnet run --project modern/TaxApp.Web)' }
    Ok "Windows $([Environment]::OSVersion.Version), PowerShell $($PSVersionTable.PSVersion)"
    $sdk = (& dotnet --version)
    if ([int]($sdk.Split('.')[0]) -lt 10) { throw ".NET SDK 10 or later is required (found $sdk). global.json pins it." }
    Ok ".NET SDK $sdk"
    Ok "git $((git --version) -replace 'git version ', '')"
    $detail = ".NET SDK $sdk"
    $code = Get-Command code -ErrorAction SilentlyContinue
    if ($code) {
        $codeVersion = (& code --version | Select-Object -First 1)
        Ok "VS Code $codeVersion"
        $detail += ", VS Code $codeVersion"
        $installed = @(& code --list-extensions --show-versions 2>$null)
        $recommended = @((Get-Content .vscode/extensions.json -Raw | ConvertFrom-Json).recommendations)
        foreach ($id in $recommended) {
            $match = $installed | Where-Object { $_ -like "$id@*" } | Select-Object -First 1
            if ($match) { Ok "extension $match" }
            else { Warn "extension $id is not installed (needed for the live run only). Install: code --install-extension $id" }
        }
        if ($installed | Where-Object { $_ -like 'ms-dotnettools.vscode-dotnet-modernize@*' }) {
            Warn 'the older "GitHub Copilot modernization for .NET" extension is installed. It is deprecated and adds a second @modernize-dotnet participant; disable it for a clean demo.'
        }
    }
    else { Warn 'VS Code ("code") is not on PATH. Everything else is checked; open the folder yourself for the live run.' }
    if (Get-Command python -ErrorAction SilentlyContinue) { Ok "python $((python --version) -replace 'Python ', '') (optional: golden master reproducibility)" }
    else { Warn 'python not found: the golden master reproducibility check will be skipped' }
    @{ Status = 'pass'; Detail = $detail }
}

Measure-Step '2. Golden master matches the independent reference calculation' {
    if (-not (Get-Command python -ErrorAction SilentlyContinue)) { Warn 'skipped: python is not installed'; return @{ Status = 'skipped'; Detail = 'python not installed' } }
    $output = python scripts/golden/new_tax_vectors.py --check
    if ($LASTEXITCODE -ne 0) { throw $output }
    Ok $output
    @{ Status = 'pass'; Detail = "$output" }
}

Measure-Step '3. VS Code configuration: every task executes, both launch configurations start' {
    $exit = Invoke-Script 'Test-VsCodeConfig.ps1'
    if ($exit -ne 0) { throw 'Test-VsCodeConfig.ps1 reported failures (see above)' }
    $legacyTests = Get-TestLine 'legacy-test'; $modernTests = Get-TestLine 'modern-test'
    if ($legacyTests) { Ok "legacy: $legacyTests" }
    if ($modernTests) { Ok "modern: $modernTests" }
    @{ Status = 'pass'; Detail = "legacy: $legacyTests | modern: $modernTests" }
}

Measure-Step '4. HTTP contract: legacy app (.NET Framework 4.8, through tools/LegacyHost)' {
    $site = Start-LegacySite
    try { Invoke-ContractFor 'legacy' $site } finally { Stop-Site $site }
}

Measure-Step '5. HTTP contract: prebaked modern reference (.NET 10)' {
    $site = Start-ModernSite
    try { Invoke-ContractFor 'modern' $site } finally { Stop-Site $site }
}

Measure-Step '6. Disposable workspace: -Reset, then -Compare as a baseline' {
    $workspace = Join-Path ([System.IO.Path]::GetTempPath()) ('taxapp-check-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    try {
        $exit = Invoke-Script 'New-DemoWorkspace.ps1' @('-Path', $workspace, '-NoOpen', '-NoClipboard')
        if ($exit -ne 0) { throw 'New-DemoWorkspace.ps1 failed' }
        $exit = Invoke-Script 'Compare-Workspace.ps1' @('-WorkspacePath', $workspace)
        if ($exit -ne 0) { throw 'Compare-Workspace.ps1 failed on the untouched workspace (it should be green)' }
        Ok 'a fresh workspace builds, passes its tests and passes the HTTP contract before any agent has touched it'
        @{ Status = 'pass'; Detail = 'baseline green' }
    }
    finally { if (Test-Path $workspace) { Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue } }
}

Measure-Step '7. -Compare on the .NET 10 reference, and the checks can fail (seeded regressions)' {
    $exit = Invoke-Script 'Compare-Workspace.ps1' @('-Target', 'modern/TaxApp.sln')
    if ($exit -ne 0) { throw 'Compare-Workspace.ps1 failed on the modern reference' }
    Ok 'the .NET 10 reference is reported as upgraded and intact'
    $exit = Invoke-Script 'Invoke-DriftDemo.ps1'
    if ($exit -ne 0) { throw 'the seeded regressions were NOT detected' }
    Ok 'round-to-even and camelCase JSON regressions are detected'
    @{ Status = 'pass'; Detail = 'reference intact; seeded regressions detected' }
}

Measure-Step '8. Documentation links' {
    $exit = Invoke-Script 'Test-DocLinks.ps1'
    if ($exit -ne 0) { throw 'broken documentation links' }
    @{ Status = 'pass'; Detail = '' }
}

# ------------------------------------------------------------------ summary ---
Write-Host ''
Write-Host ('{0,-98} {1,-8} {2,6}' -f 'Step', 'Result', 'Secs') -ForegroundColor Cyan
foreach ($row in $script:rows) {
    $colour = switch ($row.Status) { 'pass' { 'Green' } 'skipped' { 'Yellow' } default { 'Red' } }
    Write-Host ('{0,-98} {1,-8} {2,6}' -f $row.Step, $row.Status, $row.Seconds) -ForegroundColor $colour
}
Write-Host ''
Write-Host 'Not machine-verified (UI steps, see README "What is real"): the Copilot upgrade agent itself, the Keep/Undo prompts,' -ForegroundColor DarkGray
Write-Host 'the dashboard, and the VS Code debugger attaching (F5).' -ForegroundColor DarkGray

if ($Evidence) {
    $evidenceFile = Join-Path $root 'docs/evidence/verification.md'
    New-Item -ItemType Directory -Force (Split-Path $evidenceFile) | Out-Null
    $lines = @(
        '# Verification evidence',
        '',
        "Generated by ``./go.ps1 -Check -Evidence`` on $(Get-Date -Format 'yyyy-MM-dd HH:mm') (machine: Windows $([Environment]::OSVersion.Version), PowerShell $($PSVersionTable.PSVersion), .NET SDK $(& dotnet --version)).",
        'It records only what a script ran. The Copilot upgrade agent is a UI step and is **not** part of this evidence.',
        '',
        '| Step | Result | Seconds | Detail |',
        '| --- | --- | --- | --- |'
    )
    foreach ($row in $script:rows) { $lines += ('| {0} | {1} | {2} | {3} |' -f $row.Step, $row.Status, $row.Seconds, ($row.Detail -replace '\|', '/')) }
    $lines += ''
    $lines += "Overall: **$(if ($script:failed -eq 0) { 'PASS' } else { 'FAIL' })** in $([int]$checkWatch.Elapsed.TotalSeconds) seconds."
    Set-Content -LiteralPath $evidenceFile -Value $lines -Encoding utf8
    Note "wrote $($evidenceFile.Replace($root + '\', ''))"
}

if ($script:failed -eq 0) {
    Write-Host ("CHECK PASS: all steps green in {0:n0}s" -f $checkWatch.Elapsed.TotalSeconds) -ForegroundColor Green
    exit 0
}
Write-Host "CHECK FAIL: $($script:failed) step(s) failed" -ForegroundColor Red
exit 1
