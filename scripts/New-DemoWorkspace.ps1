#requires -Version 7.0
<#
.SYNOPSIS
    Creates the disposable workspace for the live demo: a clean git repository holding only the legacy Tax App.

.DESCRIPTION
    The GitHub Copilot upgrade agent changes files, creates branches and commits. So the live run happens in a throwaway copy
    outside this repository, never in the repository itself.

    The workspace contains what a real team's repository would: the solution, the web app, the tests, a short
    copilot-instructions.md and the tax-money-rules skill. It does NOT contain the prebaked modern app, the HTTP contract or
    the legacy host, so the agent sees only the code it is meant to upgrade.

    It then proves the copy builds and its tests pass, checks `git status` is clean, copies the upgrade prompt to the
    clipboard and opens VS Code on the folder.

    Safety: it refuses to delete a folder that it did not create (a marker file is written on creation).

.PARAMETER Path
    Where to create the workspace. Default: ~/tax-app-demo-workspace

.PARAMETER NoOpen
    Do not open VS Code.

.PARAMETER NoBuild
    Skip the build and test proof.

.PARAMETER NoClipboard
    Do not copy the prompt to the clipboard.
#>
[CmdletBinding()]
param(
    [string]$Path = (Join-Path $HOME 'tax-app-demo-workspace'),
    [switch]$NoOpen,
    [switch]$NoBuild,
    [switch]$NoClipboard,
    [string]$GitName = 'Tax App Demo',
    [string]$GitEmail = 'taxapp-demo@example.invalid'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/Site.ps1')
$root = Get-RepoRoot
$Path = [System.IO.Path]::GetFullPath($Path)
$marker = Join-Path $Path '.taxapp-demo-workspace'

function Step([string]$Text) { Write-Host "`n=== $Text" -ForegroundColor Cyan }
function Ok([string]$Text) { Write-Host "  OK  $Text" -ForegroundColor Green }
function Note([string]$Text) { Write-Host "      $Text" -ForegroundColor DarkGray }

if (-not $IsWindows) { throw 'The legacy Tax App targets .NET Framework 4.8 and needs Windows.' }
foreach ($tool in 'git', 'dotnet') { if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is not on PATH." } }

function Remove-WorkspaceFolder([string]$Folder) {
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try { Remove-Item -LiteralPath $Folder -Recurse -Force -ErrorAction Stop; return }
        catch {
            if ($attempt -eq 4) {
                throw "Could not delete '$Folder': $($_.Exception.Message) Close any VS Code window that has the folder open, then run this again."
            }
            if ($attempt -eq 2) {
                Note 'still locked; shutting down idle MSBuild and compiler servers (dotnet build-server shutdown)'
                & dotnet build-server shutdown 2>&1 | Out-Null
            }
            Start-Sleep -Seconds 2
        }
    }
}

Step 'Preparing the workspace folder'
if (Test-Path -LiteralPath $Path) {
    if (-not (Test-Path -LiteralPath $marker)) {
        throw "Refusing to touch '$Path': it exists and was not created by this script (no .taxapp-demo-workspace marker). Choose another -Path."
    }
    if ($root.StartsWith($Path, [StringComparison]::OrdinalIgnoreCase) -or $Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use '$Path': it overlaps this repository."
    }
    Remove-WorkspaceFolder $Path
    Ok "removed the previous workspace at $Path"
}
New-Item -ItemType Directory -Force -Path $Path | Out-Null
Set-Content -LiteralPath $marker -Value "Created by go.ps1 -Reset from $root on $(Get-Date -Format s). Safe to delete." -Encoding utf8

Step 'Copying the legacy Tax App (without build output or runtime state)'
$excludeDirs = @('bin', 'obj', 'logs', '.vs', 'TestResults')
$excludeFiles = @('deductions.json')
$robocopyArgs = @('/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/XD') + $excludeDirs + @('/XF') + $excludeFiles
& robocopy (Join-Path $root 'legacy') $Path @robocopyArgs | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
$global:LASTEXITCODE = 0

$skillSource = Join-Path $root '.github\skills'
& robocopy $skillSource (Join-Path $Path '.github\skills') /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
$global:LASTEXITCODE = 0
& robocopy (Join-Path $root 'templates\workspace') $Path /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
$global:LASTEXITCODE = 0
Ok 'copied TaxApp.sln, TaxApp.Web, TaxApp.Tests, the tax-money-rules skill and the workspace settings'

Step 'Creating the git repository'
Push-Location $Path
try {
    git init -b main 2>&1 | Out-Null
    git config --local user.name $GitName
    git config --local user.email $GitEmail
    git config --local core.autocrlf false
    git add -A
    git commit -q -m 'Initial commit: legacy Tax App (.NET Framework 4.8, ASP.NET MVC 5)' 2>&1 | Out-Null
    Ok "initial commit on main by '$GitName' (local identity only; your global git identity is untouched)"

    if (-not $NoBuild) {
        Step 'Proving the copy builds and its tests pass'
        dotnet build TaxApp.sln -nologo -v:minimal 2>&1 | Select-Object -Last 3 | ForEach-Object { Note $_ }
        if ($LASTEXITCODE -ne 0) { throw 'The workspace did not build.' }
        $testOutput = dotnet test TaxApp.sln --no-build -nologo -v:minimal 2>&1
        $summary = $testOutput | Select-String -Pattern 'Passed!|Failed!' | Select-Object -Last 1
        if ($LASTEXITCODE -ne 0) { $testOutput | Select-Object -Last 15 | ForEach-Object { Write-Host $_ }; throw 'The workspace tests failed.' }
        Ok ($summary.Line.Trim())
        $dirty = @(git status --porcelain)
        if ($dirty.Count -ne 0) { throw "git status is not clean after building: $($dirty -join '; ')" }
        Ok 'git status is clean (build output is ignored), so the agent starts from a clean tree'
    }
}
finally { Pop-Location }

$prompt = $null
$promptsFile = Join-Path $root 'PROMPTS.md'
if (Test-Path $promptsFile) {
    $match = [regex]::Match((Get-Content -LiteralPath $promptsFile -Raw), '(?s)<!-- prompt:upgrade -->\s*```text\r?\n(.*?)\r?\n```')
    if ($match.Success) { $prompt = $match.Groups[1].Value.Trim() }
}
if ($prompt -and -not $NoClipboard) {
    try { Set-Clipboard -Value $prompt; Ok 'the upgrade prompt from PROMPTS.md is on your clipboard' }
    catch { Note "could not use the clipboard: $($_.Exception.Message)" }
}

$code = Get-Command code -ErrorAction SilentlyContinue
if (-not $NoOpen) {
    if ($code) { & code --new-window $Path; Ok "opened VS Code on $Path" }
    else { Note 'VS Code ("code") is not on PATH. Open the folder yourself.' }
}

Write-Host @"

Workspace ready: $Path

Next, in VS Code (see RUN-SHEET.md):
  1. Open the Copilot Chat view and pick the Upgrade agent (or type @upgrade).
  2. Paste the prompt (it is on your clipboard) and follow the confirmations.
  3. When the agent finishes, run:  ./go.ps1 -Compare
"@
