#requires -Version 7.0
<#
.SYNOPSIS
    Checks that relative links in the Markdown files resolve, including #heading anchors.

.DESCRIPTION
    Skips external links (http, https, mailto). A link such as RUN-SHEET.md#which-agent-do-i-pick must point at an existing
    file AND at a heading in it that produces that GitHub-style anchor.
#>
[CmdletBinding()]
param([string]$Root = (Join-Path $PSScriptRoot '..'))

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path $Root).Path

function Get-Anchors([string]$Path) {
    $anchors = [System.Collections.Generic.HashSet[string]]::new()
    $inFence = $false
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*```') { $inFence = -not $inFence; continue }
        if ($inFence) { continue }
        if ($line -match '^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$') {
            $slug = $Matches[1].ToLowerInvariant() -replace '[^\p{L}\p{N}\s_-]', '' -replace '\s', '-'
            [void]$anchors.Add($slug)
        }
    }
    , $anchors
}

$files = Get-ChildItem -LiteralPath $Root -Recurse -Filter *.md -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules|\.git)\\' }

$broken = [System.Collections.Generic.List[string]]::new()
$checked = 0
foreach ($file in $files) {
    $inFence = $false
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        if ($line -match '^\s*```') { $inFence = -not $inFence; continue }
        if ($inFence) { continue }
        foreach ($m in [regex]::Matches($line, '(?<!\!)\[[^\]]*\]\(([^)\s]+)(?:\s+"[^"]*")?\)')) {
            $target = $m.Groups[1].Value
            if ($target -match '^(https?:|mailto:|<)') { continue }
            $checked++
            $pathPart, $anchor = $target -split '#', 2
            $pathPart = [uri]::UnescapeDataString(($pathPart -split '\?')[0])
            $resolved = if ($pathPart) { Join-Path $file.DirectoryName $pathPart } else { $file.FullName }
            $relative = $file.FullName.Substring($Root.Length + 1)
            if (-not (Test-Path -LiteralPath $resolved)) { $broken.Add("${relative}:${lineNumber}: missing target '$target'"); continue }
            if ($anchor -and (Test-Path -LiteralPath $resolved -PathType Leaf) -and $resolved -like '*.md') {
                if (-not (Get-Anchors $resolved).Contains($anchor.ToLowerInvariant())) { $broken.Add("${relative}:${lineNumber}: no heading for anchor '#$anchor' in '$pathPart'") }
            }
        }
    }
}

if ($broken.Count -gt 0) {
    $broken | ForEach-Object { Write-Host "  BROKEN $_" -ForegroundColor Red }
    Write-Host "DOC LINKS FAIL: $($broken.Count) broken of $checked relative links in $($files.Count) files" -ForegroundColor Red
    exit 1
}
Write-Host "DOC LINKS PASS: $checked relative links in $($files.Count) Markdown files all resolve" -ForegroundColor Green
