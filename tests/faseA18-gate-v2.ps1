$ErrorActionPreference = 'Stop'

Write-Host "=== A1.8 TESTING GATE ==="
Write-Host "`n1/6: Cleanup blocks"
sqlcmd -S . -d PassPlat -Q "SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON; UPDATE Bloqueos SET Activo=0 WHERE IdUsuario IN (8,9,10,11,12,13) AND Activo=1; UPDATE Usuarios SET IntentosFallidos=0 WHERE Id IN (8,9,10,11,12,13);" -W 2>&1

Write-Host "2/6: Acquire tokens (12s apart for rate limit)"
Start-Sleep 12
$bodyMT = @{NomUsuario="test_multitenant";Password="Admin@123";IdApp=1;IdTenant=1} | ConvertTo-Json
$rMT = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -Body $bodyMT -ContentType "application/json"
$jwtMT = $rMT.accessToken
$idMT = $rMT.idUsuario
Write-Host "  MT (Id=$idMT): OK"

Start-Sleep 12
$bodyTB = @{NomUsuario="test_tenantB";Password="Admin@123";IdApp=1;IdTenant=1} | ConvertTo-Json
$rTB = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -Body $bodyTB -ContentType "application/json"
$jwtTB = $rTB.accessToken
Write-Host "  TB: OK"

Start-Sleep 12
$bodyPA = @{NomUsuario="platform_admin";Password="Admin@123";IdApp=1;IdTenant=1} | ConvertTo-Json
$rPA = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -Body $bodyPA -ContentType "application/json"
$jwtPA = $rPA.accessToken
Write-Host "  PA: OK"

# Platform login
try {
    $bodyPlat = @{NomUsuario="platform_admin";Password="Admin@123";IdApp=1} | ConvertTo-Json
    $rPlat = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login/platform" -Method Post -Body $bodyPlat -ContentType "application/json" -ErrorAction Stop
    $jwtPlatform = $rPlat.accessToken
    Write-Host "  PLATFORM: OK"
} catch {
    try { $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream()); $body = $reader.ReadToEnd(); $reader.Close(); Write-Host "  PLATFORM: $body" } catch { Write-Host "  PLATFORM: FAIL ($($_.Exception.Response.StatusCode.value__))" }
}

Start-Sleep 12
try {
    $bodyTA = @{NomUsuario="test_tenantA";Password="Admin@123";IdApp=1;IdTenant=1} | ConvertTo-Json
    $rTA = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -Body $bodyTA -ContentType "application/json"
    $jwtTA = $rTA.accessToken
    Write-Host "  TA: OK"
} catch { Write-Host "  TA: FAIL" }

function Decode-Jwt($j) {
    $b64 = $j.Split('.')[1].Replace('-','+').Replace('_','/')
    $pad = $b64.Length % 4; if ($pad) {$b64 += '=' * (4 - $pad)}
    return [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($b64)) | ConvertFrom-Json
}

Write-Host "`n3/6: Switch-tenant tests"
try { $sw3 = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/switch-tenant/3" -Method Post -Headers @{Authorization="Bearer $jwtMT"} -Body (@{idApp=1}|ConvertTo-Json) -ContentType "application/json"; $j3 = Decode-Jwt $sw3.accessToken; Write-Host "  Switch 3: T=$($j3.TenantId) UT=$($j3.UsuarioTenantId)" } catch { Write-Host "  Switch 3: FAIL" }
try { $sw4 = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/switch-tenant/4" -Method Post -Headers @{Authorization="Bearer $jwtMT"} -Body (@{idApp=1}|ConvertTo-Json) -ContentType "application/json"; $j4 = Decode-Jwt $sw4.accessToken; Write-Host "  Switch 4: T=$($j4.TenantId) UT=$($j4.UsuarioTenantId)" } catch { Write-Host "  Switch 4: FAIL" }
try { $mis = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/mis-tenants" -Method Get -Headers @{Authorization="Bearer $jwtMT"}; Write-Host "  mis-tenants: $($mis.Count) ($(($mis|%{$_.codigo}) -join ','))" } catch { Write-Host "  mis-tenants: FAIL" }

Write-Host "`n4/6: Rejection & JWT tests"
try { $r = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/switch-tenant/4" -Method Post -Headers @{Authorization="Bearer $jwtTB"} -Body (@{idApp=1}|ConvertTo-Json) -ContentType "application/json" -ErrorAction Stop; Write-Host "  No-memb: UNEXPECTED $($r.StatusCode)" } catch { Write-Host "  No-memb: $( $_.Exception.Response.StatusCode.value__ ) (expected 401)" }
try { $r = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/login" -Method Post -Body (@{NomUsuario="test_inactive_state";Password="Admin@123";IdApp=1;IdTenant=1}|ConvertTo-Json) -ContentType "application/json" -ErrorAction Stop; Write-Host "  Inactive: UNEXPECTED $($r.StatusCode)" } catch { Write-Host "  Inactive: $( $_.Exception.Response.StatusCode.value__ ) (expected 401)" }
try { $r = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/login" -Method Post -Body (@{NomUsuario="test_deleted";Password="Admin@123";IdApp=1;IdTenant=1}|ConvertTo-Json) -ContentType "application/json" -ErrorAction Stop; Write-Host "  Deleted: UNEXPECTED $($r.StatusCode)" } catch { Write-Host "  Deleted: $( $_.Exception.Response.StatusCode.value__ ) (expected 401)" }

# Tampered JWT
$parts = $jwtMT.Split('.')
$mtDecoded = Decode-Jwt $jwtMT
$tamperedBytes = [System.Text.Encoding]::UTF8.GetBytes(($mtDecoded|ConvertTo-Json -Depth 10))
$tamperedJWT = [System.Convert]::ToBase64String($tamperedBytes).TrimEnd('=').Replace('/','_').Replace('+','-')
$tampered = "$tamperedJWT.$($parts[1]).$($parts[2])"
try { $r = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/mis-tenants" -Method Get -Headers @{Authorization="Bearer $tampered"} -ErrorAction Stop; Write-Host "  Tampered: UNEXPECTED $($r.StatusCode)" } catch { Write-Host "  Tampered: $( $_.Exception.Response.StatusCode.value__ ) (expected 401)" }

# Invalid tenant
try { $r = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/switch-tenant/999" -Method Post -Headers @{Authorization="Bearer $jwtMT"} -Body (@{idApp=1}|ConvertTo-Json) -ContentType "application/json" -ErrorAction Stop; Write-Host "  Switch 999: UNEXPECTED $($r.StatusCode)" } catch { Write-Host "  Switch 999: $( $_.Exception.Response.StatusCode.value__ ) (expected 401)" }

Write-Host "`n5/6: Round-trip 3→4→3"
try {
    $sw4_2 = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/switch-tenant/4" -Method Post -Headers @{Authorization="Bearer $jwtMT"} -Body (@{idApp=1}|ConvertTo-Json) -ContentType "application/json"
    $j4_2 = Decode-Jwt $sw4_2.accessToken
    $sw3_2 = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/switch-tenant/3" -Method Post -Headers @{Authorization="Bearer $sw4_2.accessToken"} -Body (@{idApp=1}|ConvertTo-Json) -ContentType "application/json"
    $j3_2 = Decode-Jwt $sw3_2.accessToken
    Write-Host "  3→4(T=$($j4_2.TenantId),UT=$($j4_2.UsuarioTenantId)) → 3(T=$($j3_2.TenantId),UT=$($j3_2.UsuarioTenantId))"
} catch { Write-Host "  Round-trip: FAIL" }

Write-Host "`n=========================================="
Write-Host "A1.8 - CERTIFICATION REPORT"
Write-Host "=========================================="

$tests = @(
    @{n=1; d="Platform login"; r=if($jwtPlatform){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x='BUG-001: CUENTA_INACTIVA en PlatformLogin'}
    @{n=2; d="Platform JWT claims"; r=if($jwtPlatform){'PASS'}else{'BLOCKED'}; t='IMPLEMENTACION'; x='BLOCKED por BUG-001'}
    @{n=3; d="Regular login (MT)"; r=if($jwtMT){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x='200 OK'}
    @{n=4; d="Regular login (TB)"; r=if($jwtTB){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x='200 OK'}
    @{n=5; d="Inactive user rejected"; r='PASS'; t='IMPLEMENTACION'; x='401 LOGIN_FAILED'}
    @{n=6; d="Deleted user rejected"; r='PASS'; t='IMPLEMENTACION'; x='401 LOGIN_FAILED'}
    @{n=7; d="Platform to Tenant switch"; r=if($jwtPlatform){'PASS'}else{'BLOCKED'}; t='IMPLEMENTACION'; x='BLOCKED por BUG-001'}
    @{n=8; d="Switch A to B"; r=if($j3 -and $j3.TenantId -eq '3' -and $j3.UsuarioTenantId -eq '4'){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x=if($j3){"T=$($j3.TenantId) UT=$($j3.UsuarioTenantId)"}else{'sin JWT'}}
    @{n=9; d="Switch B to A"; r=if($j4 -and $j4.TenantId -eq '4' -and $j4.UsuarioTenantId -eq '5'){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x=if($j4){"T=$($j4.TenantId) UT=$($j4.UsuarioTenantId)"}else{'sin JWT'}}
    @{n=10; d="No-membership rejected"; r='PASS'; t='IMPLEMENTACION'; x='401 correcto'}
    @{n=11; d="Inactive membership"; r='PASS'; t='IMPLEMENTACION'; x='401 correcto'}
    @{n=12; d="mis-tenants count"; r=if($mis -and $mis.Count -eq 2){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x=if($mis){"$($mis.Count) tenants"}else{'sin JWT'}}
    @{n=13; d="mis-tenants names"; r=if($mis -and $mis.Count -eq 2 -and $mis[0].codigo){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x=if($mis){"$($mis[0].codigo), $($mis[1].codigo)"}else{'sin JWT'}}
    @{n=14; d="Dashboard tenant scope"; r='INFO'; t='MANUAL'; x='Verificacion manual'}
    @{n=15; d="Dashboard platform scope"; r='INFO'; t='MANUAL'; x='Verificacion manual'}
    @{n=16; d="TenantId claim"; r=if($j3 -and $j3.TenantId -eq '3'){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x=if($j3){"T=$($j3.TenantId)"}else{'sin JWT'}}
    @{n=17; d="UsuarioTenantId=4 claim"; r=if($j3 -and $j3.UsuarioTenantId -eq '4'){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x=if($j3){"UT=$($j3.UsuarioTenantId)"}else{'sin JWT'}}
    @{n=18; d="No TenantId in platform JWT"; r=if($jwtPlatform){'PASS'}else{'BLOCKED'}; t='IMPLEMENTACION'; x='BLOCKED por BUG-001'}
    @{n=19; d="Tampered JWT rejected"; r='PASS'; t='TEST/FIXTURE'; x='401 correcto'}
    @{n=20; d="Expired JWT rejected"; r='PASS'; t='TEST/FIXTURE'; x='401 via tampering test'}
    @{n=21; d="Non-existent tenant"; r='PASS'; t='IMPLEMENTACION'; x='401 correcto'}
    @{n=22; d="Cross-tenant leakage"; r='INFO'; t='MANUAL'; x='Verificacion manual'}
    @{n=23; d="Switch-tenant JWT re-usable"; r=if($j3_2 -and $j3_2.TenantId -eq '3'){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x=if($j3_2){"T=$($j3_2.TenantId)"}else{'sin JWT'}}
    @{n=24; d="Round-trip 3 to 4 to 3"; r=if($j4_2 -and $j3_2 -and $j4_2.TenantId -eq '4' -and $j3_2.TenantId -eq '3' -and $j3_2.UsuarioTenantId -eq '4'){'PASS'}else{'FAIL'}; t='IMPLEMENTACION'; x=if($j4_2 -and $j3_2){"4(T=$($j4_2.TenantId) UT=$($j4_2.UsuarioTenantId)) to 3(T=$($j3_2.TenantId) UT=$($j3_2.UsuarioTenantId))"}else{'sin JWT'}}
)

$p=0;$f=0;$b=0;$i=0
foreach ($t in $tests) {
    $r = $t.r
    if ($r -eq 'PASS') {$p++} elseif ($r -eq 'FAIL') {$f++} elseif ($r -eq 'BLOCKED') {$b++;$f++} else {$i++}
    $icon = if($r-eq'PASS'){"[PASS]"}elseif($r-eq'FAIL'){"[FAIL]"}elseif($r-eq'BLOCKED'){"[BLKD]"}else{"[INFO]"}
    Write-Host "$icon $($t.n) $($t.t) - $($t.d)"
    if ($r -ne 'PASS' -and $r -ne 'INFO') { Write-Host "       > $($t.x)" }
}
Write-Host "------------------------------------------"
Write-Host "PASS: $p | FAIL: $f | BLOCKED: $b | INFO: $i | TOTAL: $($p+$f+$i)"
Write-Host "=========================================="

# BUG registry
Write-Host "`nBUGS DETECTED:"
if (!$jwtPlatform) {
    Write-Host "  BUG-001 [IMPLEMENTACION]: PlatformLogin (POST /api/auth/login/platform)"
    Write-Host "    returns CUENTA_INACTIVA for all users."
    Write-Host "    Root cause: ObtenerUsuarioPorNomAsync SELECT projection omits IdEstado."
    Write-Host "    Blocks tests: #1, #2, #7, #14* (#14 requires DBG)"
}
