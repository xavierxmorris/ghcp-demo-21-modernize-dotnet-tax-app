#requires -Version 7.0
<#
.SYNOPSIS
    Answers "is the business functionality still intact?" for the upgraded workspace (or any Tax App implementation).

.DESCRIPTION
    After the GitHub Copilot upgrade agent has run in the disposable workspace, this script:

      1. builds the solution and runs its tests,
      2. finds the web project and reports its target framework (net10.0 means upgraded; net48 means still the original),
      3. starts the app (a net10 app with `dotnet`, a net48 app through tools/LegacyHost), and
      4. runs the same HTTP contract (scripts/Invoke-TaxContract.ps1) that the legacy app and the prebaked
         reference pass.

    Run it BEFORE the agent to prove the baseline, and AFTER to see what drifted.

.PARAMETER WorkspacePath
    The disposable workspace. Default: ~/tax-app-demo-workspace

.PARAMETER Target
    Test something else instead: a .sln, .slnx or .csproj path (for example modern\TaxApp.sln, the prebaked reference).

.PARAMETER Vectors
    Golden master to use. Default: the tax-vectors.json found under the target, else the one in this repository.

.PARAMETER Port
    Port for the app under test. Default: a free port.
#>
[CmdletBinding()]
param(
    [string]$WorkspacePath = (Join-Path $HOME 'tax-app-demo-workspace'),
    [string]$Target,
    [string]$Vectors,
    [int]$Port = 0
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/Site.ps1')
$root = Get-RepoRoot

function Step([string]$Text) { Write-Host "`n=== $Text" -ForegroundColor Cyan }

# ------------------------------------------------------------- locate target --
if ($Target) {
    $resolved = (Resolve-Path -LiteralPath $Target).Path
    $label = $Target
}
else {
    if (-not (Test-Path -LiteralPath $WorkspacePath)) { throw "Workspace not found: $WorkspacePath. Run ./go.ps1 -Reset first." }
    $resolved = (Resolve-Path -LiteralPath $WorkspacePath).Path
    $label = "workspace $WorkspacePath"
}
$searchDir = if ((Get-Item -LiteralPath $resolved).PSIsContainer) { $resolved } else { Split-Path $resolved -Parent }
$solution = if ($resolved -match '\.(sln|slnx|csproj)$') { $resolved }
else { Get-ChildItem -LiteralPath $resolved -File | Where-Object { $_.Extension -in '.sln', '.slnx' } | Select-Object -First 1 -ExpandProperty FullName }
if (-not $solution) { throw "No .sln, .slnx or .csproj found in $resolved" }
Write-Host "Comparing: $label" -ForegroundColor Cyan
Write-Host "Solution:  $solution"

$verdicts = [ordered]@{}

# ------------------------------------------------------------- build + test ---
Step 'Build'
$buildOutput = dotnet build $solution -nologo -v:minimal 2>&1
$buildOk = ($LASTEXITCODE -eq 0)
$buildOutput | Select-Object -Last 4 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
$verdicts['build'] = if ($buildOk) { 'OK' } else { 'FAILED' }

$testOk = $false
if ($buildOk) {
    Step 'Tests'
    $testOutput = dotnet test $solution --no-build -nologo -v:minimal 2>&1
    $testOk = ($LASTEXITCODE -eq 0)
    $line = $testOutput | Select-String -Pattern 'Passed!|Failed!' | Select-Object -Last 1
    $testOutput | Select-Object -Last 4 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    $verdicts['tests'] = if ($testOk) { "OK ($($line.Line.Trim()))" } else { 'FAILED' }
}
else { $verdicts['tests'] = 'not run (build failed)' }

# --------------------------------------------------------------- web project --
Step 'Find the web project'
$projects = Get-ChildItem -LiteralPath $searchDir -Recurse -Filter *.csproj -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$web = $projects | Where-Object { Test-Path (Join-Path $_.DirectoryName 'Controllers') } | Select-Object -First 1
if (-not $web) { $web = $projects | Where-Object { $_.BaseName -match 'Web' } | Select-Object -First 1 }
if (-not $web) { throw 'Could not find a web project (a .csproj next to a Controllers folder).' }
$props = (dotnet msbuild $web.FullName -nologo -getProperty:TargetFramework -getProperty:TargetFrameworks -getProperty:TargetPath | ConvertFrom-Json).Properties
$framework = if ($props.TargetFramework) { $props.TargetFramework } else { ($props.TargetFrameworks -split ';' | Select-Object -Last 1) }
Write-Host "    $($web.FullName.Replace($searchDir, '.'))  ->  $framework"
$upgraded = $framework -match '^net(\d+)\.' -and [int]$Matches[1] -ge 8
$verdicts['target framework'] = if ($framework -like 'net10*') { "$framework (upgraded to .NET 10)" } elseif ($upgraded) { "$framework (upgraded, but not .NET 10)" } else { "$framework (NOT upgraded)" }

# --------------------------------------------------------------- run + contract
$contractOk = $false
if ($buildOk) {
    Step 'Start the app and run the HTTP contract'
    if ($Port -eq 0) { $Port = Get-FreeTcpPort }
    $vectorsPath = $Vectors
    if (-not $vectorsPath) {
        $found = Get-ChildItem -LiteralPath $searchDir -Recurse -Filter tax-vectors.json -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Select-Object -First 1
        $vectorsPath = if ($found) { $found.FullName } else { Join-Path $root 'legacy\TaxApp.Tests\Golden\tax-vectors.json' }
    }
    Write-Host "    golden master: $vectorsPath"
    $site = $null
    try {
        $site = if ($framework -like 'net4*') { Start-LegacySite -Port $Port -SitePath $web.DirectoryName }
        else { Start-ModernSite -Port $Port -ProjectDir $web.DirectoryName -AssemblyPath $props.TargetPath }
        if (Wait-HttpReady -Url $site.Url -TimeoutSec 120 -Process $site.Process) {
            $exit = Invoke-Contract -BaseUrl $site.Url -Label $label -Vectors $vectorsPath
            $contractOk = ($exit -eq 0)
            $verdicts['http contract'] = if ($contractOk) { 'PASS' } else { 'FAIL (behaviour drifted; see the failing checks above)' }
        }
        else {
            $verdicts['http contract'] = "the app did not start. See $($site.Err) and $($site.Out)"
            Get-Content -LiteralPath $site.Err -Tail 8 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        }
    }
    finally { Stop-Site $site }
}
else { $verdicts['http contract'] = 'not run (build failed)' }

# -------------------------------------------------------------------- verdict -
Write-Host ''
Write-Host 'Result' -ForegroundColor Cyan
foreach ($k in $verdicts.Keys) { Write-Host ('  {0,-18} {1}' -f $k, $verdicts[$k]) }
$intact = $buildOk -and $testOk -and $contractOk
Write-Host ''
if (-not $intact) {
    Write-Host 'NOT PROVEN: at least one of build, tests or the HTTP contract failed.' -ForegroundColor Red
}
elseif ($framework -like 'net4*') {
    Write-Host 'BASELINE GREEN: the original app builds, passes its tests and the HTTP contract. Run the upgrade, then run this again.' -ForegroundColor Yellow
}
else {
    Write-Host 'BUSINESS FUNCTIONALITY INTACT: the upgraded app builds, its tests pass and it passes the same HTTP contract as the original.' -ForegroundColor Green
}
exit ($(if ($intact) { 0 } else { 1 }))
