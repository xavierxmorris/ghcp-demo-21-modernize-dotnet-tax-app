#requires -Version 7.0
# Shared helpers for starting, probing and stopping the legacy and modern Tax App sites.
# Dot-source this file: . (Join-Path $PSScriptRoot 'lib/Site.ps1')

function Get-RepoRoot {
    (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    $port
}

function Wait-HttpReady {
    param([string]$Url, [int]$TimeoutSec = 120, $Process)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if ($Process -and $Process.HasExited) { return $false }
        try {
            $response = Invoke-WebRequest -Uri $Url -TimeoutSec 10 -SkipHttpErrorCheck -MaximumRedirection 0
            if ([int]$response.StatusCode -lt 500) { return $true }
        }
        catch { }
        Start-Sleep -Milliseconds 400
    }
    $false
}

function Stop-ProcessTree {
    param([int]$ProcessId)
    $children = Get-CimInstance Win32_Process -Filter "ParentProcessId=$ProcessId" -ErrorAction SilentlyContinue
    foreach ($child in $children) { Stop-ProcessTree -ProcessId $child.ProcessId }
    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

function Start-SiteProcess {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [string]$WorkingDirectory = (Get-Location).Path,
        [string]$Name = 'site',
        [hashtable]$Environment = @{}
    )
    $logDir = Join-Path ([System.IO.Path]::GetTempPath()) 'taxapp-demo'
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    $stamp = '{0}-{1}' -f $Name, (Get-Date -Format 'HHmmssfff')
    $out = Join-Path $logDir "$stamp.out.log"
    $err = Join-Path $logDir "$stamp.err.log"
    $quoted = @($ArgumentList | ForEach-Object { $a = [string]$_; if ($a -match '\s' -and -not $a.StartsWith('"')) { '"' + $a + '"' } else { $a } })
    $saved = @{}
    foreach ($key in $Environment.Keys) { $saved[$key] = [Environment]::GetEnvironmentVariable($key); [Environment]::SetEnvironmentVariable($key, [string]$Environment[$key]) }
    try {
        $process = Start-Process -FilePath $FilePath -ArgumentList $quoted -WorkingDirectory $WorkingDirectory -PassThru -RedirectStandardOutput $out -RedirectStandardError $err -WindowStyle Hidden
    }
    finally {
        foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
    }
    [pscustomobject]@{ Process = $process; Out = $out; Err = $err; Url = $null }
}

function Start-LegacySite {
    param([int]$Port = (Get-FreeTcpPort), [string]$SitePath)
    $root = Get-RepoRoot
    $exe = Join-Path $root 'tools\LegacyHost\bin\Debug\net48\TaxApp.LegacyHost.exe'
    $site = if ($SitePath) { $SitePath } else { Join-Path $root 'legacy\TaxApp.Web' }
    if (-not (Test-Path $exe)) { throw "Legacy host is not built. Run: dotnet build tools\LegacyHost\LegacyHost.csproj" }
    if (-not (Test-Path (Join-Path $site 'bin\TaxApp.Web.dll'))) { throw "Legacy site is not built: $site. Run: dotnet build legacy\TaxApp.sln" }
    $handle = Start-SiteProcess -FilePath $exe -ArgumentList @('--site', $site, '--port', $Port) -Name 'legacy'
    $handle.Url = "http://localhost:$Port"
    $handle
}

function Start-ModernSite {
    param([int]$Port = (Get-FreeTcpPort), [string]$ProjectDir, [string]$AssemblyPath)
    $root = Get-RepoRoot
    if (-not $ProjectDir) { $ProjectDir = Join-Path $root 'modern\TaxApp.Web' }
    if (-not $AssemblyPath) { $AssemblyPath = Join-Path $ProjectDir 'bin\Debug\net10.0\TaxApp.Web.dll' }
    if (-not (Test-Path $AssemblyPath)) { throw "Not built: $AssemblyPath. Run: dotnet build modern\TaxApp.sln" }
    $handle = Start-SiteProcess -FilePath 'dotnet' -ArgumentList @("`"$AssemblyPath`"", '--urls', "http://localhost:$Port") -WorkingDirectory $ProjectDir -Name 'modern' -Environment @{ ASPNETCORE_ENVIRONMENT = 'Development' }
    $handle.Url = "http://localhost:$Port"
    $handle
}

function Stop-Site {
    param($Site)
    if ($Site -and $Site.Process -and -not $Site.Process.HasExited) { Stop-ProcessTree -ProcessId $Site.Process.Id }
}

function Invoke-Contract {
    param([string]$BaseUrl, [string]$Label, [string]$Vectors, [string]$ReportPath, [switch]$ShowAll)
    $script = Join-Path (Split-Path $PSScriptRoot -Parent) 'Invoke-TaxContract.ps1'
    $argList = @('-NoProfile', '-File', $script, '-BaseUrl', $BaseUrl, '-Label', $Label)
    if ($Vectors) { $argList += @('-Vectors', $Vectors) }
    if ($ReportPath) { $argList += @('-ReportPath', $ReportPath) }
    if ($ShowAll) { $argList += '-ShowAll' }
    & pwsh @argList | Out-Host
    return $LASTEXITCODE
}
