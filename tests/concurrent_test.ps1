param(
    [string]$JwtToken,
    [string]$Url,
    [string]$IpTest
)

$ErrorActionPreference = "Stop"

$headers = @{ "Authorization" = "Bearer $JwtToken" }
$client = New-Object System.Net.Http.HttpClient
$client.Timeout = [System.Threading.CancellationToken]::None

$results = @()

# Start both requests at "the same time"
$taskA = [System.Threading.Tasks.Task]::Run({
    $start = Get-Date
    try {
        $resp = $client.GetAsync($Url).Result
        $end = Get-Date
        return [PSCustomObject]@{
            Name = "A"
            Start = $start
            End = $end
            StatusCode = $resp.StatusCode.value__
            ResponseBody = $resp.Content.ReadAsStringAsync().Result
            Exception = $null
            ExceptionType = $null
        }
    } catch {
        $end = Get-Date
        return [PSCustomObject]@{
            Name = "A"
            Start = $start
            End = $end
            StatusCode = 500
            ResponseBody = $_.Exception.Message
            Exception = $_.Exception.Message
            ExceptionType = $_.Exception.GetType().Name
        }
    }
})

$taskB = [System.Threading.Tasks.Task]::Run({
    $start = Get-Date
    try {
        $resp = $client.GetAsync($Url).Result
        $end = Get-Date
        return [PSCustomObject]@{
            Name = "B"
            Start = $start
            End = $end
            StatusCode = $resp.StatusCode.value__
            ResponseBody = $resp.Content.ReadAsStringAsync().Result
            Exception = $null
            ExceptionType = $null
        }
    } catch {
        $end = Get-Date
        return [PSCustomObject]@{
            Name = "B"
            Start = $start
            End = $end
            StatusCode = 500
            ResponseBody = $_.Exception.Message
            Exception = $_.Exception.Message
            ExceptionType = $_.Exception.GetType().Name
        }
    }
})

[System.Threading.Tasks.Task]::WaitAll($taskA, $taskB) | Out-Null

$resultA = $taskA.Result
$resultB = $taskB.Result

Write-Output "=== CONCURRENCE TEST RESULT ==="
Write-Output "IP Test: $IpTest"
Write-Output "Request A: Start=$($resultA.Start) End=$($resultA.End) Duration=$([math]::Round(($resultA.End - $resultA.Start).TotalMilliseconds, 2))ms Status=$($resultA.StatusCode) ExceptionType=$($resultA.ExceptionType)"
Write-Output "  Response: $($resultA.ResponseBody)"
Write-Output "Request B: Start=$($resultB.Start) End=$($resultB.End) Duration=$([math]::Round(($resultB.End - $resultB.Start).TotalMilliseconds, 2))ms Status=$($resultB.StatusCode) ExceptionType=$($resultB.ExceptionType)"
Write-Output "  Response: $($resultB.ResponseBody)"