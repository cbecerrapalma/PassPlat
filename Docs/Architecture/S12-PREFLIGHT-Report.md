# S12 — E2E Login Experience & Reproducible Environment — FASE 1 (PREFLIGHT) Report

- **Fecha**: 2026-08-01
- **Alcance**: FASE 1 PREFLIGHT de S12. Certificación del flujo real Blazor UI → App → Tenant → credenciales/OAuth → API → JWT → claims → permisos, y entorno reproducible. Sin modificar producción.
- **Estado**: FASE 1 completada. FASE 2 pendiente de aprobación del usuario.

---

## 1. Resumen ejecutivo

El entorno objetivo (Web Blazor + WebAPI + BBDD) está **operativo y reproducible**. La FASE 1.5 restableció el baseline completo de certificación:

| Suites | Resultado |
|--------|-----------|
| Build | **0 errores / 0 warnings nuevas** (2 NU1603 pre-existentes EF Design preview) |
| xUnit | **66/66** PASS |
| A1.8 + A1.9 (Playwright) | **41/41** PASS |
| fase14 (OAuth hardening) | **14/14** PASS |
| fase12 API | **17/17** PASS |
| fase12 UI | **23/23** PASS (2 skipped intencionales) |
| 03_VALIDATE (Seed) | 41/43 (2 fallos conocidos por runtime pollution, no de código) |

Gaps identificados: **P1 resuelto** (Web server levantado), **P2** (pollution runtime) clasificado TEST/DATA, **P4** verificado como YA RESUELTO en Login.razor.

---

## 2. Entregables FASE 1 (14/14)

### 2.1 Entorno reproducible — apagado/arranque

| Ítem | Estado | Evidencia |
|------|--------|-----------|
| API WebAPI | ✅ `http://localhost:5000` UP | HTTP 200 en `/api/auth/externo/proveedores`; PID listener 30368 / wrapper 4396 (`dotnet run --no-build --urls http://localhost:5000`) |
| Web Blazor | ✅ `http://localhost:5273` UP | HTTP 200, sirve index `AccessPlat`/Blazor; PID 28068 |
| WebAPI 5001/5259 (HTTPS legacy) | ⬇️ DOWN | No requerido para baseline HTTP 5000 |

**Perfiles de arranque certificados**:
- Web: `dotnet run --project PassPlat.Web --no-build --urls http://localhost:5273`
- WebAPI: `dotnet run --project PassPlat.WebAPI --no-build --urls http://localhost:5000`

### 2.2 Configuración Web ↔ API

- `PassPlat.Web\Program.cs`: HttpClient con `ApiBaseUrl`, default `https://localhost:5001`.
- `PassPlat.Web\wwwroot\appsettings.json`: `{"ApiBaseUrl": "http://localhost:5000", "AppSettings": {"AppId": 1}}` → **conexión Web→API certificada en 5273→5000**.
- `WebAPI appsettings.Development.json`: `Server=.;Database=PassPlat;User Id=sa;Password=inicio123;TrustServerCertificate=True`.
- `WebAPI appsettings.json`: JWT SecretKey/Encryption Key vacíos en archivo (rellenados en runtime/user-secrets) — revisar en FASE 3 para entorno reproducible (no exponer secretos en el repo).

### 2.3 Flujo login UI (Login.razor) — contrato P4

Máquina de estados **ya implementada** (P4 considerado resuelto):

```
Paso 1: App  → _requiereSeleccionApp=true → _selectedAppId (disabled si <=0)
Paso 2: Tenant → _requiereSeleccionTenant → _selectedTenantId
Paso 3: Método → MudRadioGroup Contraseña / OAuth
Paso 4: Credenciales → LoginAsync(username, password, Auth.AppId, rememberMe, idTenant)
```

- `_selectedAppId = _apps.Count == 1 ? _apps[0].Id : Auth.AppId` (auto-resolución single-app).
- `idTenant` se resuelve: `_resolvedTenantId ?? (_selectedTenantId > 0 ? _selectedTenantId : null)`.
- OAuth: `proveedores-login?idTenant={idTenant}` → `GET api/auth/externo/proveedores-login`.
- Contrato certificado: **la UI exige App y Tenant antes de pedir credenciales**.

### 2.4 Backend auth (contrato certificado — no modificar sin evidencia)

| Componente | Ubicación | Contrato |
|-----------|-----------|----------|
| `AuthenticationContext` | `Aplicacion\Services\Authentication\` | record: IdUsuario, IdTenant?, IdApp, IdDispositivo?, IdIp?, Origen, EsSistema, IdUsuarioTenant? |
| `AuthenticationTokenIssuer` | ídem | jti, BuildIdentityClaims + permisos, SHA256 refresh, RefreshTokenExpirationMinutes |
| `PermissionClaimBuilder` | `...\Claims\` | 3 vías: platform / IdUsuarioTenant / IdUsuario+IdTenant |
| `SessionManager` | ídem | CrearSesionAsync, RotateRefreshTokenAsync |
| `AuthController` | `WebAPI\Controllers\` | login, refresh, logout, login/platform, switch-tenant/{idTenant}, switch-to-platform, olvido-password, validar-mfa, restablecer-password. DTOs switch con `[Required] IdApp` |

### 2.5 Sincronización SPs fuente ↔ instalado (G7 / regla crítica)

**TODOS los SPs críticos IDENTICAL** (fuente `PASSWORDS SP.sql` ↔ `sys.sql_modules` BBDD real):

| SP | Líneas en fuente | Estado |
|----|------------------|--------|
| `SP_Auth_LoginExterno` | 1201-1509 (12× IdUsuarioTenant) | ✅ IDENTICAL (modify 2026-08-01 04:57) |
| `SP_Auth_Login` | 1510-1735 | ✅ IDENTICAL |
| `SP_Sesiones_Crear` | — | ✅ IDENTICAL |
| `SP_Usuario_Crear` | — | ✅ IDENTICAL |
| `SP_MFA_Validar` | — | ✅ IDENTICAL |
| `SP_TokensRest_Generar` | — | ✅ IDENTICAL |

Los "diff" iniciales fueron artefactos de: regex sin límite de palabra (`SP_Auth_Login` prefijo de `SP_Auth_LoginExterno`), banners/GO en `sys.sql_modules`, mojibake cp1252 vs UTF-8. Normalización: rango de línea del mapa GO + cuerpo `CREATE..END`. **Sin SOURCE_DESYNC ni DB_DESYNC.**

### 2.6 BBDD real (inventario de polución)

- Usuarios **93** (72 `test_*`/`hybrid_*`), **82 sin acceso**.
- UsuarioTenant 61, Accesos 13, Tenants 3, Apps 3, Roles 16, MFA 1, ConfProvIden 21, IdenExt 4, Sesiones **2590**.
- Roles: 1-4 globales (PLATFORM_*), 9-12 ABARROTES_* (tenant 3), 13-16 VESTUARIO_* (tenant 4), 17-20 PLATFORM_* duplicados tenant 1 (legado).

### 2.7 Seeds — pipeline reproducible

`D:\CODIGOS\BBDD\Seed`: 01_PRECHECK, SEED_Plataforma, SEED_Tenant, 02_VERIFY_SEED (34/34), 02_FIXUP_SEED, **03_VALIDATE (41/43**, 2 fallos runtime pollution), 04_RESET_Runtime. Re-ejecutado → reproduce exactamente 41/43.

---

## 3. Clasificación de gaps

| Gap | Descripción | Clasificación | Acción |
|-----|-------------|---------------|--------|
| **P1** | Web Blazor down | ✅ **RESUELTO** (ENVIRONMENT) | Levantar con perfil certificado (`--urls http://localhost:5273`) |
| **P2** | Pollution runtime: 72 `test_*`/`hybrid_*` + 82 sin acceso + 2590 sesiones | TEST/DATA | Evaluar `04_RESET_Runtime.sql` en FASE 11 sin borrar nada sin clasificación previa |
| **P3** | Verificar seeds desde BBDD limpia | DATA | Requiere BBDD limpia — coordinado con P2 |
| **P4** | UI debe exigir IdApp+IdTenant antes de credenciales | ✅ **YA RESUELTO** en Login.razor (máquina de estados App→Tenant→método→credenciales) | Cubrir con test E2E en FASE 2 |
| **P5** | fase12 UI (ProvIden) fallaba | ✅ **RESUELTO** (ENVIRONMENT — Web down) | Verificado 23/23 con Web up |

---

## 4. Plan S12 por prioridad (propuesta — espera confirmación)

| Prioridad | Fase | Descripción |
|-----------|------|-------------|
| **P0** | FASE 2 | E2E login real UI: App→Tenant→credenciales→JWT→claims→permisos (usuario seed existente, NO crear) |
| **P1** | FASE 3 | Entorno reproducible: apagado/arranque limpio con perfiles certificados + secrets seguros |
| **P2** | FASE 4 | Login OAuth (Google) vía UI completa con verificación en BBDD (IdenExt, Tokens, Historial, AudIdenExt) |
| **P3** | FASE 5 | Claims/permisos validados end-to-end (JWT → PermissionClaimBuilder → UI) |
| **P4** | FASE 6 | Tenant switching / switch-to-platform desde UI |
| **P5** | FASE 7 | MFA flow completo (registro + validación) |
| **P6** | FASE 8 | Tests E2E Playwright del flujo completo certificado |

## 5. Reglas respetadas

- ✅ Inspección antes de modificar (FASE 1 no modificó producción)
- ✅ No tocar producción para hacer pasar tests
- ✅ S8/S9 congelados; Issuer/ClaimBuilder/Context/SessionManager no modificados
- ✅ UI contra API REAL (5273→5000)
- ✅ Regla sincronización SPs (fuente ↔ instalado) verificada
- ✅ Baseline completo restablecido

## 6. Siguientes pasos

1. **Confirmar el plan S12 (sección 4)** para autorizar FASE 2.
2. FASE 2: E2E login UI real contra usuario seed existente.
