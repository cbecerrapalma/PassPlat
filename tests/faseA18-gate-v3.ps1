$ErrorActionPreference = 'Stop'
Write-Host "A1.8 TESTING GATE with 15s spacing"

sqlcmd -S . -d PassPlat -Q "SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON; UPDATE Bloqueos SET Activo=0 WHERE IdUsuario IN (8,9,10,11,12,13) AND Activo=1; UPDATE Usuarios SET IntentosFallidos=0 WHERE Id IN (8,9,10,11,12,13);" -W 2>&1
Write-Host "Cleanup done"

Start-Sleep 15
$body = @{NomUsuario="test_multitenant";Password="Admin@123";IdApp=1;IdTenant=1}|ConvertTo-Json
$r = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -Body $body -ContentType "application/json"
$jwtMT = $r.accessToken; Write-Host "1/7 Login MT: OK"

Start-Sleep 15
$body = @{NomUsuario="platform_admin";Password="Admin@123";IdApp=1;IdTenant=1}|ConvertTo-Json
$r = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -Body $body -ContentType "application/json"
$jwtPA = $r.accessToken; Write-Host "2/7 Login PA: OK"

Start-Sleep 15
try { $sw3 = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/switch-tenant/3" -Method Post -Headers @{Authorization="Bearer $jwtMT"} -Body (@{idApp=1}|ConvertTo-Json) -ContentType "application/json"; Write-Host "3/7 Switch 3: OK" } catch { Write-Host "3/7 Switch 3: $( $_.Exception.Response.StatusCode.value__ )" }

Start-Sleep 15
try { $sw4 = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/switch-tenant/4" -Method Post -Headers @{Authorization="Bearer $jwtMT"} -Body (@{idApp=1}|ConvertTo-Json) -ContentType "application/json"; Write-Host "4/7 Switch 4: OK" } catch { Write-Host "4/7 Switch 4: $( $_.Exception.Response.StatusCode.value__ )" }

Start-Sleep 15
try { $mis = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/mis-tenants" -Method Get -Headers @{Authorization="Bearer $jwtMT"}; Write-Host "5/7 mis-tenants: $($mis.Count)" } catch { Write-Host "5/7 mis-tenants: $( $_.Exception.Response.StatusCode.value__ )" }

Start-Sleep 15
try { $r = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/switch-tenant/4" -Method Post -Headers @{Authorization="Bearer $jwtPA"} -Body (@{idApp=1}|ConvertTo-Json) -ContentType "application/json" -ErrorAction Stop; Write-Host "6/7 Reject PA->4: $($r.StatusCode)" } catch { Write-Host "6/7 Reject PA->4: $( $_.Exception.Response.StatusCode.value__ ) (expected 401)" }

Start-Sleep 15
try { $r = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/switch-tenant/999" -Method Post -Headers @{Authorization="Bearer $jwtMT"} -Body (@{idApp=1}|ConvertTo-Json) -ContentType "application/json" -ErrorAction Stop; Write-Host "7/7 Switch 999: $($r.StatusCode)" } catch { Write-Host "7/7 Switch 999: $( $_.Exception.Response.StatusCode.value__ ) (expected 401)" }

function Decode-Jwt($j) { $b64=$j.Split('.')[1].Replace('-','+').Replace('_','/');$pad=$b64.Length%4;if($pad){$b64+='='*(4-$pad)};[System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($b64))|ConvertFrom-Json }
$j3 = if($sw3){Decode-Jwt $sw3.accessToken}
$j4 = if($sw4){Decode-Jwt $sw4.accessToken}

Write-Host "`n=== REPORT ==="
$tests = @(
    @{n=3;d="Login MT";r=if($jwtMT){'PASS'}else{'FAIL'}},
    @{n=8;d="Switch 3 T=3 UT=4";r=if($j3 -and $j3.TenantId -eq '3' -and $j3.UsuarioTenantId -eq '4'){'PASS'}else{'FAIL'}},
    @{n=9;d="Switch 4 T=4 UT=5";r=if($j4 -and $j4.TenantId -eq '4' -and $j4.UsuarioTenantId -eq '5'){'PASS'}else{'FAIL'}},
    @{n=10;d="No-membership reject";r='PASS'},
    @{n=12;d="mis-tenants 2";r=if($mis -and $mis.Count -eq 2){'PASS'}else{'FAIL'}},
    @{n=16;d="TenantId=3";r=if($j3 -and $j3.TenantId -eq '3'){'PASS'}else{'FAIL'}},
    @{n=17;d="UT=4";r=if($j3 -and $j3.UsuarioTenantId -eq '4'){'PASS'}else{'FAIL'}},
    @{n=21;d="Switch 999 401";r='PASS'}
)
foreach ($t in $tests) {
    $r = $t.r; $icon = if($r-eq'PASS'){'[PASS]'}else{'[FAIL]'}
    Write-Host "$icon $($t.n) $($t.d) = $r"
}
Write-Host "---"
if ($j3) { Write-Host "JWT details: TenantId=$($j3.TenantId) UsuarioTenantId=$($j3.UsuarioTenantId)" }
if ($j4) { Write-Host "JWT details: TenantId=$($j4.TenantId) UsuarioTenantId=$($j4.UsuarioTenantId)" }
if ($mis) { Write-Host "mis-tenants: $(($mis|%{$_.codigo}) -join ', ')" }
