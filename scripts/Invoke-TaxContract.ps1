#requires -Version 7.0
<#
.SYNOPSIS
    HTTP contract check for the Tax App. Runs against ANY implementation that serves the same URLs.

.DESCRIPTION
    The same checks run against:
      * the legacy ASP.NET MVC 5 app (through tools/LegacyHost),
      * the prebaked modern .NET 10 reference app, and
      * whatever the GitHub Copilot upgrade agent produces in the disposable workspace.
    That is how "the business functionality remains intact" becomes something you can run, not just say.

    It checks pages, static assets, golden-master tax figures (read from the page's data-testid / data-value hooks and
    from the JSON endpoint), validation, the deductions store (add, reject, delete) and the return summary.
    It deliberately does NOT compare exact error status codes where legacy and modern legitimately differ
    (a missing anti-forgery token is HTTP 500 in MVC 5 and 400 in ASP.NET Core): it asserts "rejected, nothing saved".

.PARAMETER BaseUrl
    Root URL of a running Tax App, for example http://localhost:8081

.PARAMETER Vectors
    Path to tax-vectors.json. Defaults to the golden master next to the legacy tests.

.PARAMETER Label
    Name shown in the report, for example "legacy" or "modern".

.PARAMETER ReportPath
    Optional path for a JSON report.

.PARAMETER ShowAll
    Print every check, not just failures.

.EXAMPLE
    ./scripts/Invoke-TaxContract.ps1 -BaseUrl http://localhost:8081 -Label legacy
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaseUrl,
    [string]$Vectors = (Join-Path $PSScriptRoot '..\legacy\TaxApp.Tests\Golden\tax-vectors.json'),
    [string]$Label = 'app',
    [string]$ReportPath,
    [switch]$ShowAll
)

$ErrorActionPreference = 'Stop'
$inv = [System.Globalization.CultureInfo]::InvariantCulture
$BaseUrl = $BaseUrl.TrimEnd('/')
$doc = Get-Content -LiteralPath $Vectors -Raw | ConvertFrom-Json

$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.UseCookies = $true
$handler.CookieContainer = [System.Net.CookieContainer]::new()
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(60)

$results = [System.Collections.Generic.List[object]]::new()
$group = ''

function Send([string]$Method, [string]$Path, [hashtable]$Form) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), "$BaseUrl$Path")
    if ($Form) {
        $pairs = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string, string]]]::new()
        foreach ($key in $Form.Keys) { $pairs.Add([System.Collections.Generic.KeyValuePair[string, string]]::new($key, [string]$Form[$key])) }
        $request.Content = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
    }
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    [pscustomobject]@{
        Status      = [int]$response.StatusCode
        Body        = $body
        Location    = if ($response.Headers.Location) { $response.Headers.Location.ToString() } else { $null }
        ContentType = if ($response.Content.Headers.ContentType) { $response.Content.Headers.ContentType.MediaType } else { $null }
    }
}

function Query([hashtable]$Values) {
    ($Values.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, [uri]::EscapeDataString([string]$_.Value) }) -join '&'
}

function Check([string]$Name, [bool]$Passed, [string]$Detail = '') {
    $results.Add([pscustomobject]@{ Group = $script:group; Name = $Name; Passed = $Passed; Detail = $Detail })
    if (-not $Passed) { Write-Host ("  FAIL [{0}] {1} {2}" -f $script:group, $Name, $Detail) -ForegroundColor Red }
    elseif ($ShowAll) { Write-Host ("  ok   [{0}] {1}" -f $script:group, $Name) -ForegroundColor DarkGreen }
}

function TestIdValue([string]$Html, [string]$Id) {
    $m = [regex]::Match($Html, '<[^>]*data-testid="' + [regex]::Escape($Id) + '"[^>]*data-value="([^"]*)"')
    if ($m.Success) { $m.Groups[1].Value } else { $null }
}

function TestIdText([string]$Html, [string]$Id) {
    $m = [regex]::Match($Html, 'data-testid="' + [regex]::Escape($Id) + '"[^>]*>([^<]*)<')
    if ($m.Success) { $m.Groups[1].Value.Trim() } else { $null }
}

function HasTestId([string]$Html, [string]$Id) { $Html.Contains('data-testid="' + $Id + '"') }

function Dec([string]$Text) { if ($null -eq $Text -or $Text -eq '') { $null } else { [decimal]::Parse($Text, $inv) } }

function Same([string]$Actual, [string]$Expected) {
    $a = Dec $Actual; $e = Dec $Expected
    ($null -ne $a) -and ($a -eq $e)
}

function DeductionRows([string]$Html) { [regex]::Matches($Html, 'data-testid="deduction-row"[^>]*data-id="([^"]+)"') | ForEach-Object { $_.Groups[1].Value } }

function AntiForgeryToken([string]$Html) {
    $input = [regex]::Match($Html, '<input[^>]*name="__RequestVerificationToken"[^>]*>')
    if (-not $input.Success) { return '' }
    $value = [regex]::Match($input.Value, 'value="([^"]+)"')
    if ($value.Success) { $value.Groups[1].Value } else { '' }
}

Write-Host "Tax App contract: $Label at $BaseUrl" -ForegroundColor Cyan

# ---------------------------------------------------------------- pages ------
$group = 'pages'
$homePage = Send GET '/'
Check 'GET / is 200' ($homePage.Status -eq 200) "got $($homePage.Status)"
Check 'home page names the app "Tax App"' ((TestIdText $homePage.Body 'app-title') -eq 'Tax App')
Check 'footer carries the disclaimer' (HasTestId $homePage.Body 'disclaimer')
$runtime = TestIdText $homePage.Body 'runtime'
foreach ($path in '/Tax/Estimate', '/Gst', '/Deductions', '/Return') {
    $page = Send GET $path
    Check "GET $path is 200" ($page.Status -eq 200) "got $($page.Status)"
}
$assets = @([regex]::Matches($homePage.Body, '(?:href|src)="([^"]+\.(?:css|js)[^"]*)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
Check 'layout references at least one stylesheet and one script' (($assets | Where-Object { $_ -match '\.css' }).Count -ge 1 -and ($assets | Where-Object { $_ -match '\.js' }).Count -ge 1) ($assets -join ', ')
foreach ($asset in $assets) {
    $a = Send GET $asset
    Check "asset $asset is served" ($a.Status -eq 200 -and $a.Body.Length -gt 0) "got $($a.Status)"
}
Check 'unknown URL is 404' ((Send GET '/does-not-exist').Status -eq 404)
# Moving App_Data under wwwroot (a documented behaviour of an upgrade skill) would publish the deductions file.
foreach ($private in '/App_Data/deductions.seed.json', '/App_Data/deductions.json', '/Web.config', '/appsettings.json', '/bin/TaxApp.Web.dll') {
    Check "$private is not publicly served" ((Send GET $private).Status -ge 400)
}

# ------------------------------------------------- estimate vectors (HTML) ---
$group = 'estimate (golden master, HTML)'
$fields = [ordered]@{ taxableIncome = 'taxable-income'; incomeTax = 'income-tax'; medicareLevy = 'medicare-levy'; totalTax = 'total-tax'; netIncome = 'net-income'; effectiveRate = 'effective-rate' }
foreach ($v in $doc.estimate) {
    $page = Send GET ('/Tax/Estimate?' + (Query @{ TaxYear = $v.taxYear; Income = $v.income; Deductions = $v.deductions }))
    $bad = @()
    foreach ($f in $fields.GetEnumerator()) {
        $actual = TestIdValue $page.Body $f.Value
        if (-not (Same $actual $v.expected.($f.Key))) { $bad += "$($f.Key): expected $($v.expected.($f.Key)) got '$actual'" }
    }
    Check $v.name ($page.Status -eq 200 -and $bad.Count -eq 0) ($bad -join '; ')
}

# ------------------------------------------------ estimate vectors (JSON) ----
$group = 'estimate (golden master, JSON wire contract)'
$jsonProps = [ordered]@{ TaxableIncome = 'taxableIncome'; IncomeTax = 'incomeTax'; MedicareLevy = 'medicareLevy'; TotalTax = 'totalTax'; NetIncome = 'netIncome'; EffectiveRate = 'effectiveRate' }
foreach ($v in $doc.estimate) {
    $r = Send GET ('/Tax/EstimateJson?' + (Query @{ taxYear = $v.taxYear; income = $v.income; deductions = $v.deductions }))
    $bad = @()
    if ($r.Status -ne 200) { $bad += "status $($r.Status)" }
    if ($r.ContentType -ne 'application/json') { $bad += "content type '$($r.ContentType)'" }
    foreach ($p in $jsonProps.GetEnumerator()) {
        # Case-sensitive on purpose: the JavaScript on the estimator page reads PascalCase property names.
        $m = [regex]::Match($r.Body, '"' + $p.Key + '"\s*:\s*(-?\d+(?:\.\d+)?)')
        if (-not $m.Success) { $bad += "missing property $($p.Key)" }
        elseif (-not (Same $m.Groups[1].Value $v.expected.($p.Value))) { $bad += "$($p.Key): expected $($v.expected.($p.Value)) got $($m.Groups[1].Value)" }
    }
    Check $v.name ($bad.Count -eq 0) ($bad -join '; ')
}

# ---------------------------------------------------------- GST vectors ------
$group = 'GST (golden master)'
foreach ($v in $doc.gst) {
    $page = Send GET ('/Gst?' + (Query @{ Amount = $v.amount; Mode = $v.mode }))
    $bad = @()
    foreach ($pair in @(@('gst-amount', 'gstAmount'), @('amount-excl-gst', 'amountExclGst'), @('amount-incl-gst', 'amountInclGst'))) {
        $actual = TestIdValue $page.Body $pair[0]
        if (-not (Same $actual $v.expected.($pair[1]))) { $bad += "$($pair[1]): expected $($v.expected.($pair[1])) got '$actual'" }
    }
    Check $v.name ($page.Status -eq 200 -and $bad.Count -eq 0) ($bad -join '; ')
}

# ------------------------------------------------------------ validation -----
$group = 'validation'
foreach ($case in @(
        @{ Name = 'negative income shows a validation message and no result'; Path = '/Tax/Estimate?Income=-5'; ResultId = 'total-tax' },
        @{ Name = 'non-numeric income shows a validation message and no result'; Path = '/Tax/Estimate?Income=abc'; ResultId = 'total-tax' },
        @{ Name = 'unsupported tax year shows a validation message and no result'; Path = '/Tax/Estimate?TaxYear=1999-00&Income=100'; ResultId = 'total-tax' },
        @{ Name = 'negative GST amount shows a validation message and no result'; Path = '/Gst?Amount=-1'; ResultId = 'gst-amount' },
        @{ Name = 'non-numeric return income shows a validation message and no result'; Path = '/Return?Income=abc'; ResultId = 'rs-total-tax' })) {
    $page = Send GET $case.Path
    Check $case.Name ($page.Status -eq 200 -and (HasTestId $page.Body 'validation-summary') -and -not (HasTestId $page.Body $case.ResultId)) "status $($page.Status)"
}
$blank = Send GET '/Tax/Estimate'
Check 'blank estimator form has no result and no validation noise' ($blank.Status -eq 200 -and -not (HasTestId $blank.Body 'total-tax') -and -not (HasTestId $blank.Body 'validation-summary'))
Check 'JSON endpoint: negative income is 400' ((Send GET '/Tax/EstimateJson?income=-1').Status -eq 400)
Check 'JSON endpoint: missing income is 400' ((Send GET '/Tax/EstimateJson').Status -eq 400)
Check 'JSON endpoint: unsupported tax year is 400' ((Send GET '/Tax/EstimateJson?taxYear=1999-00&income=100').Status -eq 400)

# ----------------------------------------------------------- deductions ------
$group = 'deductions (store, anti-forgery, encoding)'
$list = Send GET '/Deductions'
$baselineRows = @(DeductionRows $list.Body)
$baselineTotal = Dec (TestIdValue $list.Body 'deductions-total')
Check 'deductions page shows a total' ($null -ne $baselineTotal)
$token = AntiForgeryToken $list.Body
Check 'add form carries an anti-forgery token' ($token.Length -gt 20)

$probeIds = @()
try {
    $before = Send POST '/Deductions/Add' @{ Description = 'No token probe'; Category = 'Other'; Amount = '1.00' }
    $afterNoToken = Send GET '/Deductions'
    Check 'POST without an anti-forgery token is rejected and saves nothing' ($before.Status -ge 400 -and @(DeductionRows $afterNoToken.Body).Count -eq $baselineRows.Count) "status $($before.Status)"

    $bad = Send POST '/Deductions/Add' @{ __RequestVerificationToken = $token; Description = ''; Category = 'Other'; Amount = '-5' }
    $afterBad = Send GET '/Deductions'
    Check 'invalid deduction is redisplayed with a validation message and saves nothing' ($bad.Status -eq 200 -and (HasTestId $bad.Body 'validation-summary') -and @(DeductionRows $afterBad.Body).Count -eq $baselineRows.Count) "status $($bad.Status)"

    $add = Send POST '/Deductions/Add' @{ __RequestVerificationToken = $token; Description = 'Contract & probe'; Category = 'HomeOffice'; Amount = '12.34' }
    Check 'valid POST redirects back to the list' (($add.Status -in (301, 302, 303, 307, 308)) -and ($add.Location -match '/Deductions')) "status $($add.Status) location $($add.Location)"
    $afterAdd = Send GET '/Deductions'
    $rowsAfterAdd = @(DeductionRows $afterAdd.Body)
    $probeIds = @($rowsAfterAdd | Where-Object { $_ -notin $baselineRows })
    Check 'the new deduction appears in the list' ($rowsAfterAdd.Count -eq $baselineRows.Count + 1 -and $probeIds.Count -eq 1)
    Check 'the total increases by exactly the amount added' ((Dec (TestIdValue $afterAdd.Body 'deductions-total')) -eq ($baselineTotal + 12.34d))
    Check 'user text is HTML-encoded' ($afterAdd.Body.Contains('Contract &amp; probe') -and -not $afterAdd.Body.Contains('Contract & probe'))

    $returnWithProbe = Send GET ('/Return?' + (Query @{ TaxYear = '2026-27'; Income = '95000'; Withheld = '20000' }))
    Check 'return summary uses the saved deductions total' ((Dec (TestIdValue $returnWithProbe.Body 'rs-deductions')) -eq ($baselineTotal + 12.34d))
}
finally {
    foreach ($id in $probeIds) { $null = Send POST '/Deductions/Delete' @{ __RequestVerificationToken = $token; id = $id } }
}
$final = Send GET '/Deductions'
Check 'removing the probe restores the original list and total' (@(DeductionRows $final.Body).Count -eq $baselineRows.Count -and (Dec (TestIdValue $final.Body 'deductions-total')) -eq $baselineTotal)

# -------------------------------------------------------- return summary -----
$group = 'return summary'
foreach ($case in @(@{ Year = '2026-27'; Income = '95000'; Withheld = '20000' }, @{ Year = '2025-26'; Income = '60000'; Withheld = '5000' }, @{ Year = '2026-27'; Income = '18000'; Withheld = '0' })) {
    $summary = Send GET ('/Return?' + (Query @{ TaxYear = $case.Year; Income = $case.Income; Withheld = $case.Withheld }))
    $deductions = Dec (TestIdValue $summary.Body 'rs-deductions')
    $estimate = Send GET ('/Tax/Estimate?' + (Query @{ TaxYear = $case.Year; Income = $case.Income; Deductions = $deductions.ToString($inv) }))
    $total = Dec (TestIdValue $estimate.Body 'total-tax')
    $balance = [decimal]$case.Withheld - $total
    $expectedWord = if ($balance -ge 0) { 'Refund' } else { 'Payable' }
    $bad = @()
    if ((Dec (TestIdValue $summary.Body 'rs-total-tax')) -ne $total) { $bad += "total tax differs from the estimator ($total)" }
    if ((TestIdText $summary.Body 'rs-result') -ne $expectedWord) { $bad += "expected '$expectedWord' got '$(TestIdText $summary.Body 'rs-result')'" }
    if ((Dec (TestIdValue $summary.Body 'rs-result-amount')) -ne [math]::Abs($balance)) { $bad += "amount expected $([math]::Abs($balance)) got $(TestIdValue $summary.Body 'rs-result-amount')" }
    if ((Dec (TestIdValue $summary.Body 'rs-withheld')) -ne [decimal]$case.Withheld) { $bad += 'withheld not echoed' }
    Check "$($case.Year): income $($case.Income), withheld $($case.Withheld) -> $expectedWord" ($summary.Status -eq 200 -and $bad.Count -eq 0) ($bad -join '; ')
}

# --------------------------------------------------------------- report ------
$passed = @($results | Where-Object Passed).Count
$failed = $results.Count - $passed
Write-Host ''
$results | Group-Object Group | ForEach-Object {
    $p = @($_.Group | Where-Object Passed).Count
    $color = if ($p -eq $_.Count) { 'Green' } else { 'Red' }
    Write-Host ('  {0,-46} {1}/{2}' -f $_.Name, $p, $_.Count) -ForegroundColor $color
}
Write-Host ''
if ($runtime) { Write-Host "  Runtime reported by the app: $runtime" -ForegroundColor DarkGray }
$verdict = if ($failed -eq 0) { 'PASS' } else { 'FAIL' }
Write-Host ("CONTRACT {0}: {1} ({2} of {3} checks passed)" -f $verdict, $Label, $passed, $results.Count) -ForegroundColor $(if ($failed -eq 0) { 'Green' } else { 'Red' })

if ($ReportPath) {
    [pscustomobject]@{ label = $Label; baseUrl = $BaseUrl; runtime = $runtime; checked = (Get-Date).ToString('s'); passed = $passed; total = $results.Count; checks = $results } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8
}
$client.Dispose()
exit ($(if ($failed -eq 0) { 0 } else { 1 }))
