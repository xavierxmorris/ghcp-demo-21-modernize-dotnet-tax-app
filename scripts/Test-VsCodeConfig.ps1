#requires -Version 7.0
<#
.SYNOPSIS
    Proves the VS Code configuration in .vscode/ works, without opening VS Code.

.DESCRIPTION
    VS Code cannot be driven from a script, so this does the next best thing and executes what VS Code would execute:

      * parses tasks.json, launch.json, extensions.json and settings.json
      * checks labels, dependsOn targets, ${input:} references and paths
      * RUNS every task exactly as written (command + args + env + cwd). Background tasks (the two "run site" tasks) are
        started and must print their problemMatcher endsPattern, then they are stopped
      * for each launch configuration: runs the preLaunchTask, checks the program exists, starts it with the configured
        args and checks that serverReadyAction.pattern matches what it really prints
      * checks that every recommended extension exists on the Visual Studio Marketplace

    Not covered (needs a human in VS Code): the debugger attaching, breakpoints, the Copilot agent UI.
    Tasks whose label starts with "demo:" or that prompt for input are validated structurally but not run,
    because they open windows or need a workspace.

.PARAMETER Static
    Structure checks only; do not run any task.

.PARAMETER SkipMarketplace
    Do not query the Marketplace for extension ids.

.PARAMETER BackgroundTimeoutSec
    How long to wait for a background task to print its endsPattern.
#>
[CmdletBinding()]
param(
    [switch]$Static,
    [switch]$SkipMarketplace,
    [int]$BackgroundTimeoutSec = 120
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/Site.ps1')
$root = Get-RepoRoot
$vsDir = Join-Path $root '.vscode'
$logDir = Join-Path ([System.IO.Path]::GetTempPath()) 'taxapp-demo'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$script:results = [System.Collections.Generic.List[object]]::new()
$script:done = [System.Collections.Generic.HashSet[string]]::new()

function Check([string]$Name, [bool]$Passed, [string]$Detail = '') {
    $script:results.Add([pscustomobject]@{ Name = $Name; Passed = $Passed; Detail = $Detail })
    if ($Passed) { Write-Host "  ok   $Name" -ForegroundColor DarkGreen }
    else { Write-Host "  FAIL $Name $Detail" -ForegroundColor Red }
}

function Skip([string]$Name, [string]$Why) { Write-Host "  skip $Name ($Why)" -ForegroundColor DarkYellow }

function Read-Json([string]$Name) { Get-Content -LiteralPath (Join-Path $vsDir $Name) -Raw | ConvertFrom-Json -Depth 30 }

function Resolve-Variables([string]$Text) { $Text.Replace('${workspaceFolder}', $root) }

function Test-NotRunnable($Task) {
    $Task.label -like 'demo:*' -or (($Task | ConvertTo-Json -Depth 10) -match '\$\{input:')
}

function Test-CommandAvailable([string]$Name) { [bool](Get-Command $Name -ErrorAction SilentlyContinue) }

function Wait-LogPattern([string]$Path, [string]$Pattern, $Process, [int]$TimeoutSec) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $text = if (Test-Path $Path) { Get-Content -LiteralPath $Path -Raw -ErrorAction SilentlyContinue } else { '' }
        if ($text -and ($text -match $Pattern)) { return $true }
        if ($Process.HasExited) {
            $text = if (Test-Path $Path) { Get-Content -LiteralPath $Path -Raw -ErrorAction SilentlyContinue } else { '' }
            return [bool]($text -and ($text -match $Pattern))
        }
        Start-Sleep -Milliseconds 400
    }
    $false
}

function Invoke-VsTask([string]$Label) {
    if (-not $script:done.Add($Label)) { return $true }
    $task = $script:tasks | Where-Object label -eq $Label | Select-Object -First 1
    if (-not $task) { Check "task '$Label' exists" $false; return $false }

    $ok = $true
    foreach ($dependency in @($task.dependsOn)) {
        if ($dependency -and -not (Invoke-VsTask $dependency)) { $ok = $false }
    }
    if (-not $task.command) { return $ok }
    if (Test-NotRunnable $task) { Skip "task '$Label'" 'opens a window or prompts for input'; return $ok }
    if ($task.command -eq 'python' -and -not (Test-CommandAvailable 'python')) { Skip "task '$Label'" 'python is not installed'; return $ok }
    if (-not $ok) { Skip "task '$Label'" 'a dependency failed'; return $false }

    $cwd = if ($task.options -and $task.options.cwd) { Resolve-Variables $task.options.cwd } else { $root }
    $arguments = @($task.args | ForEach-Object { Resolve-Variables ([string]$_) })
    $environment = @{}
    if ($task.options -and $task.options.env) { foreach ($p in $task.options.env.PSObject.Properties) { $environment[$p.Name] = [string]$p.Value } }
    $logName = ($Label -replace '[^\w]+', '-')

    if ($task.isBackground) {
        $pattern = $task.problemMatcher.background.endsPattern
        $handle = Start-SiteProcess -FilePath $task.command -ArgumentList $arguments -WorkingDirectory $cwd -Name $logName -Environment $environment
        try {
            $ready = Wait-LogPattern $handle.Out $pattern $handle.Process $BackgroundTimeoutSec
            $detail = if ($ready) { '' } else { "never printed /$pattern/ (port 8081 or 5081 already in use?). Log: $($handle.Out)" }
            Check "task '$Label' starts and prints its endsPattern" $ready $detail
            if ($ready) {
                $url = [regex]::Match((Get-Content -LiteralPath $handle.Out -Raw), 'https?://localhost:\d+').Value
                $answered = $url -and (Wait-HttpReady -Url $url -TimeoutSec 60 -Process $handle.Process)
                Check "task '$Label' serves HTTP at $url" $answered
            }
        }
        finally { Stop-Site $handle }
        return $ready
    }

    $log = Join-Path $logDir "$logName.log"
    $saved = @{}
    foreach ($key in $environment.Keys) { $saved[$key] = [Environment]::GetEnvironmentVariable($key); [Environment]::SetEnvironmentVariable($key, $environment[$key]) }
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    Push-Location $cwd
    try {
        & $task.command @arguments *> $log
        $code = $LASTEXITCODE
    }
    finally {
        Pop-Location
        foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
    }
    $tail = if ($code -ne 0 -and (Test-Path $log)) { (Get-Content -LiteralPath $log -Tail 5) -join ' | ' } else { '' }
    Check ("task '$Label' exits 0 ({0:n0}s)" -f $watch.Elapsed.TotalSeconds) ($code -eq 0) "exit $code. $tail"
    return ($code -eq 0)
}

Write-Host "VS Code configuration check ($(if ($Static) { 'static' } else { 'static + executed' }))" -ForegroundColor Cyan

# ------------------------------------------------------------------ parse ----
Write-Host "`n.vscode files" -ForegroundColor Cyan
$parsed = @{}
foreach ($file in 'tasks.json', 'launch.json', 'extensions.json', 'settings.json') {
    try { $parsed[$file] = Read-Json $file; Check "$file is valid JSON" $true }
    catch { Check "$file is valid JSON" $false $_.Exception.Message }
}
if ($parsed.Count -lt 4) { Write-Host "Cannot continue." -ForegroundColor Red; exit 1 }

# ------------------------------------------------------------------ tasks ----
Write-Host "`ntasks.json" -ForegroundColor Cyan
$script:tasks = @($parsed['tasks.json'].tasks)
$labels = @($script:tasks.label)
Check 'task labels are unique' (($labels | Sort-Object -Unique).Count -eq $labels.Count)
$inputIds = @($parsed['tasks.json'].inputs | ForEach-Object { $_.id })
foreach ($task in $script:tasks) {
    foreach ($dependency in @($task.dependsOn)) {
        if ($dependency) { Check "'$($task.label)' dependsOn '$dependency' exists" ($dependency -in $labels) }
    }
    if ($task.command) {
        Check "'$($task.label)' has a type of process or shell" ($task.type -in 'process', 'shell')
        foreach ($input in ([regex]::Matches(($task | ConvertTo-Json -Depth 10), '\$\{input:([^}]+)\}') | ForEach-Object { $_.Groups[1].Value })) {
            Check "'$($task.label)' input '$input' is declared" ($input -in $inputIds)
        }
        foreach ($arg in @($task.args)) {
            $resolved = Resolve-Variables ([string]$arg)
            if ($resolved -match '^[A-Za-z]:[\\/]' -and $resolved -notmatch '[*?]') {
                Check "'$($task.label)' path exists: $($resolved.Replace($root, '.'))" (Test-Path -LiteralPath $resolved)
            }
        }
    }
    if ($task.isBackground) {
        Check "'$($task.label)' defines a background endsPattern" ([bool]$task.problemMatcher.background.endsPattern)
    }
}

# ----------------------------------------------------------------- settings --
Write-Host "`nsettings.json and extensions.json" -ForegroundColor Cyan
$defaultSolution = $parsed['settings.json'].'dotnet.defaultSolution'
if ($defaultSolution -and $defaultSolution -ne 'disable') {
    Check "dotnet.defaultSolution exists ($defaultSolution)" (Test-Path -LiteralPath (Join-Path $root $defaultSolution))
}
$recommended = @($parsed['extensions.json'].recommendations) + @($parsed['extensions.json'].unwantedRecommendations)
foreach ($id in $recommended) { Check "extension id looks valid: $id" ($id -match '^[A-Za-z0-9][\w-]*\.[A-Za-z0-9][\w.-]*$') }
if (-not $SkipMarketplace -and -not $Static) {
    foreach ($id in $recommended) {
        $body = @{ filters = @(@{ criteria = @(@{ filterType = 7; value = $id }) }); flags = 914 } | ConvertTo-Json -Depth 6
        try {
            $reply = Invoke-RestMethod -Method Post -Uri 'https://marketplace.visualstudio.com/_apis/public/gallery/extensionquery' -ContentType 'application/json' -Headers @{ Accept = 'application/json;api-version=7.1-preview.1' } -Body $body -TimeoutSec 30
            $found = $reply.results[0].extensions | Select-Object -First 1
            Check "Marketplace has $id" ([bool]$found) 'not found'
        }
        catch { Skip "Marketplace lookup for $id" "network: $($_.Exception.Message)" }
    }
}

# ------------------------------------------------------------ run the tasks ---
if (-not $Static) {
    Write-Host "`nExecuting tasks exactly as VS Code would" -ForegroundColor Cyan
    foreach ($label in $labels) { $null = Invoke-VsTask $label }
}

# ----------------------------------------------------------------- launch -----
Write-Host "`nlaunch.json" -ForegroundColor Cyan
foreach ($config in @($parsed['launch.json'].configurations)) {
    $name = $config.name
    Check "'$name' has type, request and name" ($config.type -and $config.request -and $config.name)
    if ($config.preLaunchTask) { Check "'$name' preLaunchTask '$($config.preLaunchTask)' exists" ($config.preLaunchTask -in $labels) }
    if ($Static) { continue }

    if ($config.preLaunchTask) {
        $null = Invoke-VsTask $config.preLaunchTask
    }
    $program = Resolve-Variables ([string]$config.program)
    Check "'$name' program exists after its preLaunchTask: $($program.Replace($root, '.'))" (Test-Path -LiteralPath $program)
    if (-not (Test-Path -LiteralPath $program)) { continue }

    $cwd = if ($config.cwd) { Resolve-Variables ([string]$config.cwd) } else { $root }
    Check "'$name' cwd exists" (Test-Path -LiteralPath $cwd)
    $arguments = @($config.args | ForEach-Object { Resolve-Variables ([string]$_) })
    $environment = @{}
    if ($config.env) { foreach ($p in $config.env.PSObject.Properties) { $environment[$p.Name] = [string]$p.Value } }

    if ($config.type -eq 'coreclr' -and $program -like '*.dll') {
        $handle = Start-SiteProcess -FilePath 'dotnet' -ArgumentList (@($program) + $arguments) -WorkingDirectory $cwd -Name 'launch' -Environment $environment
    }
    else {
        $handle = Start-SiteProcess -FilePath $program -ArgumentList $arguments -WorkingDirectory $cwd -Name 'launch' -Environment $environment
    }
    try {
        $pattern = $config.serverReadyAction.pattern
        if ($pattern) {
            $ready = Wait-LogPattern $handle.Out $pattern $handle.Process $BackgroundTimeoutSec
            Check "'$name' prints output matching its serverReadyAction pattern" $ready "pattern /$pattern/. Log: $($handle.Out)"
            if ($ready) {
                $url = [regex]::Match((Get-Content -LiteralPath $handle.Out -Raw), $pattern).Groups[1].Value
                Check "'$name' serves HTTP at $url" ([bool]$url -and (Wait-HttpReady -Url $url -TimeoutSec 60 -Process $handle.Process))
            }
        }
    }
    finally { Stop-Site $handle }
}

# ---------------------------------------------------------------- summary -----
$failed = @($script:results | Where-Object { -not $_.Passed })
Write-Host ''
$colour = if ($failed.Count -eq 0) { 'Green' } else { 'Red' }
Write-Host ("VS CODE CONFIG {0}: {1} of {2} checks passed" -f $(if ($failed.Count -eq 0) { 'PASS' } else { 'FAIL' }), ($script:results.Count - $failed.Count), $script:results.Count) -ForegroundColor $colour
exit ($(if ($failed.Count -eq 0) { 0 } else { 1 }))
