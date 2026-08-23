param(
    [string]$Jwt
)

$ErrorActionPreference = "Continue"
$endpoint = "http://localhost:5259/api/dispconfiables/trigger-new-ip/3?ip=203.0.113.57"

Write-Output "=== CONCURRENCE TEST START ==="
Write-Output "Endpoint: $endpoint"
Write-Output "Pre-check: IP 203.0.113.57 should not exist in DB"

# Create two separate HttpClient instances with separate handlers for true concurrency
$h1 = New-Object System.Net.Http.HttpClientHandler
$h2 = New-Object System.Net.Http.HttpClientHandler
$c1 = New-Object System.Net.Http.HttpClient($h1)
$c2 = New-Object System.Net.Http.HttpClient($h2)

$authHeader = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $Jwt)
$c1.DefaultRequestHeaders.Authorization = $authHeader
$c2.DefaultRequestHeaders.Authorization = $authHeader

# Create barrier to ensure both requests start simultaneously
$barrier = New-Object System.Threading.Barrier(2)

$t1 = [System.Threading.Tasks.Task]::Factory.StartNew(async {
    $barrier.SignalAndWait(5000)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = $c1.GetAsync($endpoint).Result
        $sw.Stop()
        $body = $resp.Content.ReadAsStringAsync().Result
        return @{
            Name = "A"
            StatusCode = [int]$resp.StatusCode
            Body = $body
            ElapsedMs = $sw.ElapsedMilliseconds
            Error = $null
            ErrorType = $null
        }
    } catch {
        $sw.Stop()
        return @{
            Name = "A"
            StatusCode = -1
            Body = $_.Exception.Message
            ElapsedMs = $sw.ElapsedMilliseconds
            Error = $_.Exception.Message
            ErrorType = $_.Exception.GetType().Name
            InnerError = if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $null }
            InnerErrorType = if ($_.Exception.InnerException) { $_.Exception.InnerException.GetType().Name } else { $null }
        }
    }
})

$t2 = [System.Threading.Tasks.Task]::Factory.StartNew(async {
    $barrier.SignalAndWait(5000)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = $c2.GetAsync($endpoint).Result
        $sw.Stop()
        $body = $resp.Content.ReadAsStringAsync().Result
        return @{
            Name = "B"
            StatusCode = [int]$resp.StatusCode
            Body = $body
            ElapsedMs = $sw.ElapsedMilliseconds
            Error = $null
            ErrorType = $null
        }
    } catch {
        $sw.Stop()
        return @{
            Name = "B"
            StatusCode = -1
            Body = $_.Exception.Message
            ElapsedMs = $sw.ElapsedMilliseconds
            Error = $_.Exception.Message
            ErrorType = $_.Exception.GetType().Name
            InnerError = if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $null }
            InnerErrorType = if ($_.Exception.InnerException) { $_.Exception.InnerException.GetType().Name } else { $null }
        }
    }
})

[System.Threading.Tasks.Task]::WaitAll($t1, $t2) | Out-Null

$ra = $t1.Result
$rb = $t2.Result

Write-Output ""
Write-Output "=== RESULT ==="
Write-Output "Request A: StatusCode=$($ra.StatusCode) ElapsedMs=$($ra.ElapsedMs)"
Write-Output "  Body=$($ra.Body)"
Write-Output "  Error=$($ra.Error)"
Write-Output "  ErrorType=$($ra.ErrorType)"
if ($ra.InnerError) { Write-Output "  InnerError=$($ra.InnerError)" }
if ($ra.InnerErrorType) { Write-Output "  InnerErrorType=$($ra.InnerErrorType)" }
Write-Output ""
Write-Output "Request B: StatusCode=$($rb.StatusCode) ElapsedMs=$($rb.ElapsedMs)"
Write-Output "  Body=$($rb.Body)"
Write-Output "  Error=$($rb.Error)"
Write-Output "  ErrorType=$($rb.ErrorType)"
if ($rb.InnerError) { Write-Output "  InnerError=$($rb.InnerError)" }
if ($rb.InnerErrorType) { Write-Output "  InnerErrorType=$($rb.InnerErrorType)" }

$c1.Dispose()
$c2.Dispose()
$h1.Dispose()
$h2.Dispose()
