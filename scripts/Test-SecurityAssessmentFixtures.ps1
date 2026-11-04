#requires -Version 7.0
<#
.SYNOPSIS
    Proves that intentional application findings are assessment-visible but remain private and unrouted.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/Site.ps1')
$root = Get-RepoRoot
$solution = Join-Path $root 'legacy\TaxApp.sln'
$webProject = Join-Path $root 'legacy\TaxApp.Web\TaxApp.Web.csproj'
$webConfig = Join-Path $root 'legacy\TaxApp.Web\Web.config'
$sampleSource = Join-Path $root 'legacy\TaxApp.Web\Helpers\SecurityAssessmentSamples.cs'
$webOutput = Join-Path $root 'legacy\TaxApp.Web\bin'

function Assert-Assessment([string]$Name, [bool]$Passed) {
    if (-not $Passed) { throw "Security assessment fixture check failed: $Name" }
    Write-Host "  ok   $Name" -ForegroundColor DarkGreen
}

[xml]$webXml = Get-Content -LiteralPath $webProject -Raw
$packages = @($webXml.Project.ItemGroup.PackageReference)
$vulnerablePackage = @(
    $packages | Where-Object {
        $_.Include -eq 'System.Data.SqlClient' -and $_.Version -eq '4.8.5'
    }
)
Assert-Assessment 'web project carries the intentional vulnerable direct package' ($vulnerablePackage.Count -eq 1)

$config = Get-Content -LiteralPath $webConfig -Raw
Assert-Assessment 'Web.config carries the intentionally fake credential' (
    $config -match 'SecurityDemoPassword.*DEMO_ONLY_NOT_A_REAL_SECRET'
)

$source = Get-Content -LiteralPath $sampleSource -Raw
$signals = @(
    @{ Name = 'hard-coded credential'; Pattern = 'SecurityDemoPassword\s*=\s*"DEMO_ONLY_NOT_A_REAL_SECRET"' },
    @{ Name = 'unsafe deserialization'; Pattern = 'BinaryFormatter\(\)\.Deserialize' },
    @{ Name = 'XXE-capable XML parser'; Pattern = 'DtdProcessing\.Parse' },
    @{ Name = 'SQL string concatenation'; Pattern = 'SELECT \* FROM Deductions.*\+ category \+ ' },
    @{ Name = 'unencoded HTML sink'; Pattern = 'new HtmlString' },
    @{ Name = 'path traversal sink'; Pattern = 'File\.ReadAllText\(Path\.Combine' },
    @{ Name = 'weak SHA-1 hashing'; Pattern = 'SHA1\.Create' },
    @{ Name = 'insecure randomness'; Pattern = 'new Random\(\)' },
    @{ Name = 'insecure cookie flags'; Pattern = 'HttpOnly\s*=\s*false' }
)
foreach ($signal in $signals) {
    Assert-Assessment "$($signal.Name) sample is compiled into TaxApp.Web" ($source -match $signal.Pattern)
}
Assert-Assessment 'assessment sample has no public or controller entry point' (
    $source -notmatch 'public\s+(static\s+)?class|Controller'
)
Assert-Assessment 'assessment sample exposes no public or protected members' (
    $source -notmatch '\b(?:public|protected)\b'
)
$privateMethods = @([regex]::Matches($source, '\bprivate\s+static\s+(?:\w+(?:<[^>]+>)?)\s+\w+\('))
Assert-Assessment 'all assessment sample methods are private' (
    $privateMethods.Count -eq 8 -and
    @([regex]::Matches($source, '\b(?:public|internal|protected)\s+static\s+(?:\w+(?:<[^>]+>)?)\s+\w+\(')).Count -eq 0
)

$otherWebSources = @(
    Get-ChildItem -LiteralPath (Join-Path $root 'legacy\TaxApp.Web') -Recurse -File -Include *.cs,*.cshtml |
        Where-Object { $_.FullName -ne $sampleSource } |
        Where-Object { (Get-Content -LiteralPath $_.FullName -Raw) -match 'SecurityAssessmentSamples' }
)
Assert-Assessment 'assessment sample has no callers in the web project' ($otherWebSources.Count -eq 0)

$buildOutput = & dotnet build $solution -nologo -v:minimal 2>&1
if ($LASTEXITCODE -ne 0) {
    $buildOutput | Select-Object -Last 20 | ForEach-Object { Write-Host $_ }
    throw 'The legacy assessment solution did not build.'
}
Assert-Assessment 'web output includes the vulnerable dependency for audit' (
    Test-Path -LiteralPath (Join-Path $webOutput 'System.Data.SqlClient.dll')
)

$auditOutput = & dotnet list $webProject package --vulnerable --include-transitive --format json 2>&1
if ($LASTEXITCODE -ne 0) {
    $auditOutput | Select-Object -Last 20 | ForEach-Object { Write-Host $_ }
    throw 'NuGet vulnerability audit did not complete.'
}
try {
    $audit = ($auditOutput -join [Environment]::NewLine) | ConvertFrom-Json
}
catch {
    throw "NuGet vulnerability audit returned invalid JSON: $($auditOutput -join ' ')"
}
$auditedPackage = @(
    $audit.projects.frameworks.topLevelPackages | Where-Object {
        $_.id -eq 'System.Data.SqlClient' -and $_.resolvedVersion -eq '4.8.5'
    }
)
Assert-Assessment 'NuGet audit reports the web dependency as vulnerable' (
    $auditedPackage.Count -eq 1 -and @($auditedPackage.vulnerabilities).Count -ge 1
)

Write-Host 'ASSESSMENT FIXTURES PASS: 10 classic signals are compiled into TaxApp.Web, private, and unrouted.' -ForegroundColor Green
