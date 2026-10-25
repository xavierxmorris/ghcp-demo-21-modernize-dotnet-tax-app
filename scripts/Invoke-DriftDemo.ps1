#requires -Version 7.0
<#
.SYNOPSIS
    "What if the upgrade silently changes behaviour?" Seeds two realistic regressions into a scratch copy and shows the checks catch them.

.DESCRIPTION
    A passing check only means something if it can fail. This copies the prebaked modern app to a scratch folder, injects two
    regressions that a framework upgrade really can cause, and runs Compare-Workspace.ps1 against the result:

      1. Rounding drifts from "midpoint away from zero" to .NET's default round-to-even (0.045 becomes 0.04, not 0.05).
      2. JSON property names drift from PascalCase to camelCase (the ASP.NET Core default), which breaks the page's script.

    The scratch copy is deleted afterwards. The repository is never modified.

    Exit code 0 means the regressions WERE detected (the oracle works). Exit code 1 means they slipped through.
#>
[CmdletBinding()]
param([switch]$Keep)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/Site.ps1')
$root = Get-RepoRoot
$scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('taxapp-drift-' + [guid]::NewGuid().ToString('N').Substring(0, 8))

try {
    Write-Host "Scratch copy: $scratch" -ForegroundColor DarkGray
    & robocopy (Join-Path $root 'modern') (Join-Path $scratch 'modern') /E /XD bin obj /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
    $global:LASTEXITCODE = 0
    New-Item -ItemType Directory -Force (Join-Path $scratch 'legacy\TaxApp.Tests\Golden') | Out-Null
    Copy-Item (Join-Path $root 'legacy\TaxApp.Tests\Golden\tax-vectors.json') (Join-Path $scratch 'legacy\TaxApp.Tests\Golden') -Force

    $calculator = Join-Path $scratch 'modern\TaxApp.Web\Services\TaxCalculator.cs'
    $program = Join-Path $scratch 'modern\TaxApp.Web\Program.cs'
    $before = (Get-Content -LiteralPath $calculator -Raw), (Get-Content -LiteralPath $program -Raw)

    $rounded = $before[0].Replace('MidpointRounding.AwayFromZero', 'MidpointRounding.ToEven')
    $camel = $before[1].Replace('options.JsonSerializerOptions.PropertyNamingPolicy = null;', '// (naming policy left at the ASP.NET Core default: camelCase)')
    if ($rounded -eq $before[0] -or $camel -eq $before[1]) { throw 'The seeding patterns no longer match the source. Update Invoke-DriftDemo.ps1.' }
    Set-Content -LiteralPath $calculator -Value $rounded -NoNewline -Encoding utf8
    Set-Content -LiteralPath $program -Value $camel -NoNewline -Encoding utf8
    Write-Host 'Seeded: round-to-even in TaxCalculator, camelCase JSON in Program.cs' -ForegroundColor Yellow

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Compare-Workspace.ps1') -Target (Join-Path $scratch 'modern\TaxApp.sln') | Out-Host
    $detected = ($LASTEXITCODE -ne 0)
}
finally {
    if (-not $Keep -and (Test-Path -LiteralPath $scratch)) { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host ''
if ($detected) {
    Write-Host 'DRIFT DETECTED, as it should be: the seeded regressions were caught by the tests and the HTTP contract.' -ForegroundColor Green
    exit 0
}
Write-Host 'DRIFT MISSED: the seeded regressions passed. The oracle is broken.' -ForegroundColor Red
exit 1
