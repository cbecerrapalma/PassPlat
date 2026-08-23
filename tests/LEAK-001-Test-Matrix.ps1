# LEAK-001.x Test Matrix — Cross-tenant isolation verification
# Requires: API running at http://localhost:5000

$api = "http://localhost:5000/api"
$pass = "Admin@123"
$testUserT3 = "test_tenantA"
$testUserT4 = "test_tenantB"
$testUserPlatform = "platform_admin"

$passed = 0
$failed = 0
$errors = @()

function Test-Step {
    param($Name, $ScriptBlock)
    try {
        $result = & $ScriptBlock
        if ($result -eq $true) {
            Write-Host "  ✅ PASS: $Name" -ForegroundColor Green
            $script:passed++
        } else {
            Write-Host "  ❌ FAIL: $Name — $result" -ForegroundColor Red
            $script:failed++
            $script:errors += "$Name — $result"
        }
    } catch {
        Write-Host "  ❌ FAIL: $Name — EXCEPTION: $($_.Exception.Message)" -ForegroundColor Red
        $script:failed++
        $script:errors += "$Name — $($_.Exception.Message)"
    }
}

function Login($username, $tenantId) {
    $body = @{ NomUsuario = $username; Password = $pass; IdApp = 1; IdTenant = $tenantId } | ConvertTo-Json
    $response = Invoke-RestMethod -Uri "$api/auth/login" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
    return $response.accessToken
}

function PlatformLogin($username) {
    $body = @{ NomUsuario = $username; Password = $pass } | ConvertTo-Json
    try {
        $response = Invoke-RestMethod -Uri "$api/auth/login/platform" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
        return $response.accessToken
    } catch {
        return $null
    }
}

function Get($uri, $token) {
    $headers = @{ Authorization = "Bearer $token" }
    try {
        $response = Invoke-RestMethod -Uri $uri -Method Get -Headers $headers -ErrorAction Stop
        return @{ Success = $true; Data = $response }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
        return @{ Success = $false; StatusCode = $statusCode; Body = $body }
    }
}

function GetStatusCode($uri, $token) {
    $headers = @{ Authorization = "Bearer $token" }
    try {
        $response = Invoke-WebRequest -Uri $uri -Method Get -Headers $headers -ErrorAction Stop
        return $response.StatusCode
    } catch {
        return [int]$_.Exception.Response.StatusCode
    }
}

function Put($uri, $token, $body) {
    $headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }
    $json = $body | ConvertTo-Json
    try {
        $response = Invoke-WebRequest -Uri $uri -Method Put -Headers $headers -Body $json -ErrorAction Stop
        return $response.StatusCode
    } catch {
        return [int]$_.Exception.Response.StatusCode
    }
}

Write-Host "`n==============================================" -ForegroundColor Cyan
Write-Host "  LEAK-001.x — Cross-tenant Isolation Tests" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

# ── Login phase ─────────────────────────────────────────────
Write-Host "`n[LOGIN] Getting tokens..." -ForegroundColor Yellow

try {
    $jwtT3 = Login $testUserT3 3
    Write-Host "  T3 JWT obtained" -ForegroundColor Green
} catch {
    Write-Host "  T3 login FAILED: $_" -ForegroundColor Red
    $failed++
    $errors += "T3 Login failed"
}

try {
    $jwtT4 = Login $testUserT4 4
    Write-Host "  T4 JWT obtained" -ForegroundColor Green
} catch {
    Write-Host "  T4 login FAILED: $_" -ForegroundColor Red
    $failed++
    $errors += "T4 Login failed"
}

try {
    $jwtPlatform = PlatformLogin $testUserPlatform
    if ($jwtPlatform) {
        Write-Host "  Platform JWT obtained" -ForegroundColor Green
    } else {
        Write-Host "  Platform login FAILED" -ForegroundColor Red
        $failed++
        $errors += "Platform Login failed"
    }
} catch {
    Write-Host "  Platform login FAILED: $_" -ForegroundColor Red
    $failed++
    $errors += "Platform Login failed"
}

# ── LEAK-001.1: T3 getAll scoped to T3 only ────────────────
Write-Host "`n[LEAK-001.1] T3 GET /api/usuarios → scoped to T3 only" -ForegroundColor Yellow
Test-Step -Name "T3 GET /api/usuarios returns OK" -ScriptBlock {
    $result = Get "$api/usuarios" $jwtT3
    return $result.Success -eq $true
}
Test-Step -Name "T3 GET /api/usuarios returns only usuarios in T3" -ScriptBlock {
    $result = Get "$api/usuarios" $jwtT3
    if ($result.Success -ne $true) { return "API call failed" }
    $users = $result.Data
    if ($users.Count -eq 0) { return "Empty user list" }
    # User 5 (admin_vestuario) is in tenant 4 — should NOT appear
    $leaked = $users | Where-Object { $_.id -eq 5 } | Select-Object -First 1
    if ($leaked) { return "User 5 (tenant 4) leaked into T3 results" }
    return $true
}

# ── LEAK-001.2: T3 cannot GET user from T4 ────────────────
Write-Host "`n[LEAK-001.2] T3 GET /api/usuarios/5 (T4 user) → 404" -ForegroundColor Yellow
Test-Step -Name "T3 GET /api/usuarios/5 returns 404" -ScriptBlock {
    $sc = GetStatusCode "$api/usuarios/5" $jwtT3
    return $sc -eq 404 -or $sc -eq 403  # Allow 404 or 403
}

# ── LEAK-001.3: T3 cannot GET system user (no membership) ─
Write-Host "`n[LEAK-001.3] T3 GET /api/usuarios/1 (sistema, no UsuarioTenant in T3) → 404" -ForegroundColor Yellow
Test-Step -Name "T3 GET /api/usuarios/1 returns 404" -ScriptBlock {
    $sc = GetStatusCode "$api/usuarios/1" $jwtT3
    return $sc -eq 404 -or $sc -eq 403
}

# ── LEAK-001.4: T3 cannot PUT user from T4 ────────────────
Write-Host "`n[LEAK-001.4] T3 PUT /api/usuarios/5 (T4 user) → 404" -ForegroundColor Yellow
Test-Step -Name "T3 PUT /api/usuarios/5 returns 404" -ScriptBlock {
    $body = @{ Id = 5; Nombre = "hacker" }
    $sc = Put "$api/usuarios/5" $jwtT3 $body
    return $sc -eq 404 -or $sc -eq 403
}

# ── LEAK-001.5: T3 cannot DELETE user from T4 ──────────────
Write-Host "`n[LEAK-001.5] T3 DELETE /api/usuarios/5 (T4 user) → 404" -ForegroundColor Yellow
Test-Step -Name "T3 DELETE /api/usuarios/5 returns 404" -ScriptBlock {
    $headers = @{ Authorization = "Bearer $jwtT3" }
    try {
        $response = Invoke-WebRequest -Uri "$api/usuarios/5" -Method Delete -Headers $headers -ErrorAction Stop
        return $false  # Should not succeed
    } catch {
        $sc = [int]$_.Exception.Response.StatusCode
        return $sc -eq 404 -or $sc -eq 403
    }
}

# ── LEAK-001.6: Platform GET /api/usuarios returns ALL ────
Write-Host "`n[LEAK-001.6] Platform GET /api/usuarios → returns ALL" -ForegroundColor Yellow
Test-Step -Name "Platform GET /api/usuarios returns OK" -ScriptBlock {
    $result = Get "$api/usuarios" $jwtPlatform
    return $result.Success -eq $true
}
Test-Step -Name "Platform GET /api/usuarios returns users across tenants" -ScriptBlock {
    $result = Get "$api/usuarios" $jwtPlatform
    if ($result.Success -ne $true) { return "API call failed" }
    $users = $result.Data
    if ($users.Count -lt 3) { return "Expected multiple users across tenants, got $($users.Count)" }
    return $true
}

# ── LEAK-001.7: Platform can GET any user by ID ────────────
Write-Host "`n[LEAK-001.7] Platform GET /api/usuarios/5 (T4 user) → accessible" -ForegroundColor Yellow
Test-Step -Name "Platform GET /api/usuarios/5 returns user data" -ScriptBlock {
    $result = Get "$api/usuarios/5" $jwtPlatform
    if ($result.Success -ne $true) { return "API call failed: $($result.StatusCode)" }
    $user = $result.Data
    if ($user.id -ne 5) { return "Expected user id=5, got id=$($user.id)" }
    return $true
}

# ── LEAK-001.8: T4 cannot GET T3 user ──────────────────────
Write-Host "`n[LEAK-001.8] T4 GET /api/usuarios/19 (T3 user) → 404" -ForegroundColor Yellow
Test-Step -Name "T4 GET /api/usuarios/19 returns 404" -ScriptBlock {
    $sc = GetStatusCode "$api/usuarios/19" $jwtT4
    return $sc -eq 404 -or $sc -eq 403
}

# ── Summary ─────────────────────────────────────────────────
Write-Host "`n==============================================" -ForegroundColor Cyan
Write-Host "  RESULTS" -ForegroundColor Cyan
Write-Host "  Passed: $passed" -ForegroundColor Green
Write-Host "  Failed: $failed" -ForegroundColor $(if($failed -eq 0){"Green"}else{"Red"})
Write-Host "==============================================" -ForegroundColor Cyan

if ($errors.Count -gt 0) {
    Write-Host "`nFailed Tests:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

if ($failed -eq 0) {
    Write-Host "`n🎉 ALL LEAK-001 TESTS PASSED" -ForegroundColor Green
} else {
    Write-Host "`n❌ $failed TESTS FAILED" -ForegroundColor Red
}
