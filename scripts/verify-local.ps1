<#
.SYNOPSIS
  End-to-end verification of a running CodeJudge API (System Design §2 acceptance criteria).

.EXAMPLE
  .\scripts\verify-local.ps1
  .\scripts\verify-local.ps1 -BaseUrl http://localhost:5000 -ApiKey dev-api-key-change-me
#>
param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$ApiKey = "dev-api-key-change-me",
    [int]$PollSeconds = 2,
    [int]$PollLimitSeconds = 30
)

$ErrorActionPreference = "Stop"
$script:Pass = 0

function Ok($msg) { $script:Pass++; Write-Host "  [PASS] $msg" }
function Fail($msg, $detail) { Write-Host "  [FAIL] $msg"; Write-Host "         $detail"; exit 1 }

function ConvertTo-JsonOrNull($text) {
    # /health answers plain text ("Healthy"), everything else is an ApiResult JSON envelope.
    try { return ($text | ConvertFrom-Json) } catch { return $null }
}

function Invoke-Api {
    param([string]$Method, [string]$Path, [string]$Body = $null, [switch]$NoKey)
    $headers = @{ "Content-Type" = "application/json" }
    if (-not $NoKey) { $headers["X-Api-Key"] = $ApiKey }
    $args = @{ Uri = "$BaseUrl$Path"; Method = $Method; Headers = $headers; UseBasicParsing = $true }
    if ($Body) { $args["Body"] = $Body }   # Windows PowerShell 5.1 rejects a body (even $null) on GET
    try {
        $resp = Invoke-WebRequest @args
    } catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { throw }
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
        return @{ Status = [int]$resp.StatusCode; Body = $content; Json = (ConvertTo-JsonOrNull $content); Headers = $resp.Headers }
    }
    return @{ Status = [int]$resp.StatusCode; Body = $resp.Content; Json = (ConvertTo-JsonOrNull $resp.Content); Headers = $resp.Headers }
}

function Submit($Language, $Code, $ProblemId = "sum-two-numbers") {
    $payload = @{ userId = "demo"; problemId = $ProblemId; language = $Language; code = $Code } | ConvertTo-Json -Compress
    $r = Invoke-Api POST "/api/v1/submissions" $payload
    if ($r.Status -ne 201) { Fail "POST $Language -> 201" "got $($r.Status): $($r.Body)" }
    if ($r.Json.status -ne $true) { Fail "POST $Language ApiResult.status true" $r.Body }
    if ($r.Json.data.status -ne "pending") { Fail "POST $Language data.status pending" $r.Body }
    if (-not ($r.Headers["Location"] -match "/api/v1/submissions/")) { Fail "POST $Language Location header" ($r.Headers | Out-String) }
    return $r.Json.data.id
}

function Wait-Completed($Id) {
    $waited = 0
    while ($true) {
        $r = Invoke-Api GET "/api/v1/submissions/$Id"
        $st = $r.Json.data.status
        if ($st -eq "completed" -or $st -eq "error") { return $r.Json }
        if ($waited -ge $PollLimitSeconds) { Fail "submission $Id completes within ${PollLimitSeconds}s" "last status: $st" }
        Start-Sleep -Seconds $PollSeconds
        $waited += $PollSeconds
    }
}

function Show-Results($Json) {
    foreach ($r in $Json.data.results) {
        $msg = if ($r.message) { $r.message.Substring(0, [Math]::Min(60, $r.message.Length)) } else { "" }
        Write-Host ("         {0,-9} passed={1,-5} skipped={2,-5} tests={3}/{4}  {5}" -f $r.rubricItem, $r.passed, $r.skipped, $r.testsPassed, $r.testsTotal, $msg)
    }
}

function Assert-AllPassed($Name, $Json) {
    if ($Json.data.status -ne "completed") { Fail "$Name status completed" ($Json | ConvertTo-Json -Depth 6) }
    $allPassed = @($Json.data.results | Where-Object { -not $_.passed }).Count -eq 0
    if (-not $allPassed) { Show-Results $Json; Fail "$Name all three rubric items passed" "" }
    $test = $Json.data.results | Where-Object { $_.rubricItem -eq "test" }
    if ($test.testsPassed -ne 4) { Show-Results $Json; Fail "$Name testsPassed = 4" "" }
}

Write-Host "CodeJudge verification against $BaseUrl"

Write-Host "a) GET /health"
$r = Invoke-Api GET "/health" -NoKey
if ($r.Status -eq 200) { Ok "health 200" } else { Fail "health 200" "got $($r.Status): $($r.Body)" }

Write-Host "b) POST python submission"
$pyId = Submit "python" "def sum_two(a, b):`n    return a + b"
Ok "201 pending, Location header, id=$pyId"

Write-Host "c) poll python submission"
$j = Wait-Completed $pyId; Assert-AllPassed "python" $j; Ok "python completed, 4/4"; Show-Results $j

Write-Host "d) javascript submission"
$jsId = Submit "javascript" "function sumTwo(a, b) { return a + b; }"
$j = Wait-Completed $jsId; Assert-AllPassed "javascript" $j; Ok "javascript completed, 4/4"; Show-Results $j

Write-Host "e) csharp submission"
$csId = Submit "csharp" "public static class Solution { public static int Sum(int a, int b) => a + b; }"
$j = Wait-Completed $csId; Assert-AllPassed "csharp" $j; Ok "csharp completed, 4/4"; Show-Results $j

Write-Host "f) restricted keyword"
$secId = Submit "python" "import os`nos.system('ls')`ndef sum_two(a, b):`n    return a + b"
$j = Wait-Completed $secId
if ($j.data.status -ne "completed") { Fail "security case completed" ($j | ConvertTo-Json -Depth 6) }
$security = $j.data.results | Where-Object { $_.rubricItem -eq "security" }
$others = @($j.data.results | Where-Object { $_.rubricItem -ne "security" })
if ($security.passed) { Show-Results $j; Fail "security.passed=false" "" }
if (@($others | Where-Object { -not $_.skipped }).Count -ne 0) { Show-Results $j; Fail "compiles/test skipped" "" }
Ok "security failed, other two skipped"; Show-Results $j

Write-Host "g) error shapes"
$r = Invoke-Api POST "/api/v1/submissions" '{"userId":"demo","problemId":"nope","language":"python","code":"x = 1"}'
if ($r.Status -eq 400 -and $r.Json.error.errorCode -eq 3001) { Ok "unknown problem -> 400/3001" } else { Fail "unknown problem -> 400/3001" "got $($r.Status): $($r.Body)" }
$r = Invoke-Api POST "/api/v1/submissions" '{"userId":"demo","problemId":"sum-two-numbers","language":"python","code":"x = 1"}' -NoKey
if ($r.Status -eq 401 -and $r.Json.error.errorCode -eq 4001) { Ok "no api key -> 401/4001" } else { Fail "no api key -> 401/4001" "got $($r.Status): $($r.Body)" }
$r = Invoke-Api GET "/api/v1/submissions/$([guid]::NewGuid())"
if ($r.Status -eq 404 -and $r.Json.error.errorCode -eq 2001) { Ok "random id -> 404/2001" } else { Fail "random id -> 404/2001" "got $($r.Status): $($r.Body)" }
$r = Invoke-Api POST "/api/v1/submissions" '{"userId":"demo","problemId":"sum-two-numbers","language":"cobol","code":"x = 1"}'
$code = $r.Json.error.errorCode
if ($r.Status -eq 400 -and ($code -eq 3002 -or $code -eq 1000)) { Ok "language cobol -> 400/$code" } else { Fail "language cobol -> 400/3002|1000" "got $($r.Status): $($r.Body)" }

Write-Host "h) catalog and user history"
$r = Invoke-Api GET "/api/v1/problems"
if ($r.Status -eq 200 -and @($r.Json.data).Count -eq 4) { Ok "problems -> 200, 4 problems" } else { Fail "problems -> 200 with 4" "got $($r.Status): $($r.Body)" }
$r = Invoke-Api GET "/api/v1/users/demo/submissions?page=1&pageSize=10"
$ids = @($r.Json.data.items | ForEach-Object { $_.id })
foreach ($id in @($pyId, $jsId, $csId, $secId)) {
    if ($ids -notcontains $id) { Fail "user history contains $id" $r.Body }
}
if ($r.Status -eq 200) { Ok "user history -> 200 containing the 4 submissions" } else { Fail "user history -> 200" "got $($r.Status)" }

Write-Host ""
Write-Host "All $script:Pass checks passed."
