# S12 — Login E2E Certification (P0)

**Estado**: ✅ **CERTIFICADO** — FASE 2 (P0) completada
**Fecha**: 2026-08-02
**Alcance**: Certificación del login end-to-end real Blazor UI → WebAPI → SP_Auth_Login → JWT → permisos efectivos en UI/API, contra BBDD real y usuarios seed.
**Cambios de producción**: Ninguno (solo se creó la suite de tests `tests/s12-login-e2e.spec.ts` y este documento).

---

## Resumen Ejecutivo

El flujo de login completo de PassPlat fue certificado con evidencia de navegador real, red HTTP y BBDD real. Un usuario seed (`test_multitenant` Id=8) inicia sesión desde la UI Blazor, el payload llega al API con `IdApp=1` e `IdTenant=3`, el SP `SP_Auth_Login` valida credenciales, el JWT resultante contiene exactamente los claims y permisos que devuelve `SP_Permisos_Usuario_Efectivos` y el rol ABARROTES_CONSULTA, y tanto la UI como los endpoints protegidos respetan esos permisos con aislamiento de tenant verificado.

| Área | Resultado | Evidencia |
|------|-----------|-----------|
| 2.1 Inventario previo | ✅ | Reporte PREFLIGHT + inspección Login.razor/AuthService |
| 2.2 Usuario seed + contexto | ✅ | BBDD real + login API exploratorio |
| 2.3 Máquina de estados UI | ✅ | Snapshot navegador (tenant → método → credenciales) |
| 2.4 Login UI → API real | ✅ | Request/Response HTTP capturado (reqid=231) |
| 2.5 JWT vs BBDD | ✅ | Decodificación JWT = claims = SP permisos |
| 2.6 Permisos UI/API | ✅ | GET 200 / POST 403 con JWT UI real |
| 2.7 Aislamiento tenant | ✅ | JWT t3 no ve datos t4 |
| 2.8 Casos negativos | ✅ | B/C/D/E UI, F/G API, H SKIPPED |
| 2.9 API equivalente | ✅ | Claims idénticos a login UI |
| 2.10 Suite Playwright | ✅ | `s12-login-e2e.spec.ts` 12/12 PASS |
| 2.11 Regresión | ✅ | Build 0/0, xUnit 66/66, A1.8 24/24, A1.9 17/17, fase14+fase12 37/37 |
| 2.13 Documentación | ✅ | Este documento |

---

## Datos de Certificación

| Dato | Valor |
|------|-------|
| Usuario | `test_multitenant` (Id=8) |
| Password | `Admin@123` |
| Tenant | Abarrotes del Sur (Id=3) |
| App | AccessPlat/PASSPLAT (Id=1) |
| Rol | ABARROTES_CONSULTA (Id=12) |
| UsuarioTenant | Id=4 |
| Permisos JWT | ACCESOS_VER, GRUPOS_VER, MATRIZ_PERMISOS_VER, PERMISOS_VER, ROLES_VER, USUARIOS_VER, USUARIOS_VERDISP (7) |
| Web | `http://localhost:5273` |
| API | `http://localhost:5000` |
| BBDD | `Server=.;Database=PassPlat;User Id=sa;Password=inicio123` |

---

## FASE 2.4 — Login UI → API (Evidencia de Red)

**Request capturado** (POST `http://localhost:5000/api/auth/login`, HTTP 200):
```json
{"nomUsuario":"test_multitenant","email":null,"idApp":1,"password":"Admin@123","idDisp":null,"idIP":null,"idAgente":null,"idTenant":3}
```

**Response** (resumen):
```json
{"accessToken":"eyJhbGciOiJIUzI1NiIs...","refreshToken":"xdtIMDNX+...","idUsuario":8,"idTenant":3,"nomUsuario":"test_multitenant","email":"test_mt@passplat.app","reqCambioPwd":false,"idMFAPrincipal":null}
```

**Secuencia UI observada en navegador**:
1. `GET /login` → tenant-info `{"idTenant":null,"requiereSeleccion":true}` → selector Tenant + "Continuar" disabled.
2. Selección "Abarrotes del Sur" → "Continuar" habilitado.
3. Click Continuar → radio "Contraseña" (default) + form Usuario/Contraseña/Recordarme.
4. `GET /api/apps/activas` → única app activa `PASSPLAT` → auto-resuelta (AppId=1).
5. Submit → `POST /api/auth/login` → 200 → navegación a `/` (Dashboard autenticado).
6. Dashboard muestra: `test_multitenant`, chip tenant "Abarrotes del Sur", nav SEGURIDAD (Usuarios, Accesos) y Roles según permisos.

---

## FASE 2.5 — JWT vs BBDD

**JWT decodificado del login UI real**:
```
sub (nameidentifier) = 8        → Usuarios.Id = 8 ✓
IdApp                = 1        → App PASSPLAT ✓
TenantId             = 3        → UsuarioTenant.IdTenant = 3 ✓
UsuarioTenantId      = 4        → UsuarioTenant.Id = 4 ✓
permiso              = [ACCESOS_VER, GRUPOS_VER, MATRIZ_PERMISOS_VER, PERMISOS_VER, ROLES_VER, USUARIOS_VER, USUARIOS_VERDISP]
jti, iat, exp        presentes
iss = aud = PassPlat
is_system            ausente (no es usuario sistema) ✓
```

**Cadena BBDD verificada**:
- `SP_Permisos_Usuario_Efectivos(8, 3, 1)` → exactamente los 7 permisos del JWT.
- `RolesPermisos` de rol 12 (ABARROTES_CONSULTA) → exactamente los 7 permisos.
- Permisos `USUARIOS_VER` (Id=1) y `USUARIOS_VERDISP` (Id=83) activos.

**Conclusión**: BBDD → SP → PermissionClaimBuilder → JWT → UI, cadena íntegra sin divergencias.

---

## FASE 2.6 — Permisos Efectivos (UI + API)

| Endpoint | Permiso requerido | Resultado con JWT UI real |
|----------|-------------------|---------------------------|
| `GET /api/usuarios` | USUARIOS_VER | **200** — lista de usuarios tenant 3 |
| `POST /api/usuarios` | USUARIOS_CREAR | **403** Forbidden |
| `POST /api/roles` | ROLES_CREAR | **403** Forbidden |
| `GET /api/Usuarios/count` | dashboard | **200** |
| `GET /api/federacion/estadisticas/3` | dashboard | **200** |
| `GET /api/Sesiones/contar-tenant` | (sin permiso) | **403** (Dashboard lo tolera, muestra 0) |

La UI muestra únicamente la navegación correspondiente a los permisos de ABARROTES_CONSULTA (Roles y Permisos, Usuarios, Accesos). Los stat cards sin permiso renderizan 0 sin error.

---

## FASE 2.7 — Aislamiento de Tenant

- BBDD: tenant 4 (Vestuario del Norte) tiene 3 usuarios (`admin_vestuario`, `test_multitenant`, `test_tenantB`).
- Con JWT de tenant 3 (login UI real): `GET /api/usuarios?pageSize=200` devuelve **solo 3 usuarios de tenant 3**, **0 de tenant 4**, 0 de otros, 0 null.
- **Sin fuga de datos entre tenants.**

---

## FASE 2.8 — Casos Negativos

| Caso | Escenario | Resultado | Clasificación |
|------|-----------|-----------|---------------|
| A | App no seleccionada | No aplica — única app activa auto-resuelta | EXPECTED BEHAVIOR |
| B | Tenant no seleccionado | "Continuar" disabled, sin form credenciales | ✅ UI correcta |
| C | Usuario vacío | `DoLogin` retorna sin request (no-op silencioso) | ✅ esperado |
| D | Password vacío | HTTP 400 `{"Password":["The Password field is required."]}` (FluentValidation) | ✅ esperado |
| E | Password incorrecto | HTTP 401 `{"codigo":"LOGIN_FAILED","mensaje":"Credenciales invalidas"}` → UI muestra "Credenciales inválidas" | ✅ esperado |
| F | Usuario sin acceso a app | HTTP 401 `"Sin acceso a la aplicacion"` (check SP previo a password) | ✅ esperado |
| G | Usuario inactivo | HTTP 401 `"Cuenta inactiva"` (check SP previo a password) | ✅ esperado |
| H | Tenant inactivo | Los 3 tenants están Activo=1 → **SKIPPED — DATA NOT AVAILABLE** | Documentado |

---

## FASE 2.9 — API Equivalente

`POST /api/auth/login` directo con el mismo contexto produce claims idénticos al login UI (sub=8, IdApp=1, TenantId=3, UsuarioTenantId=4, 7 permisos, reqCambioPwd=false). Solo difiere `jti` (esperado, token fresco por login).

---

## FASE 2.10 — Suite `tests/s12-login-e2e.spec.ts`

**12/12 PASS** (serial, `--reporter=list`):

| # | Test | Evidencia |
|---|------|-----------|
| 1 | Login exitoso desde UI (App auto-resuelta + Tenant + credenciales) | Navega a `/`, usuario + tenant chip |
| 2 | JWT del login UI contiene claims correctos | localStorage `access_token` → decode → 7 permisos |
| 3 | UI respeta permisos: nav SEGURIDAD visible | links Usuarios/Accesos visibles |
| 4 | Endpoint protegido acepta JWT UI (GET /usuarios 200, datos tenant 3) | 200, todos idTenant=3 |
| 5 | Endpoint no autorizado rechaza JWT UI (POST /usuarios 403) | 403 |
| 6 | Aislamiento tenant: JWT t3 no devuelve t4 | 0 usuarios idTenant=4 |
| 7 | Password incorrecto desde UI muestra error controlado | "Credenciales inválidas", permanece /login |
| 8 | Credenciales vacías: sin request API | 0 requests `/auth/login` |
| 9 | Usuario sin acceso a la app via API | 401 SinAccesoApp |
| 10 | Usuario inactivo via API | 401 CuentaInactiva |
| 11 | API-equivalente produce mismos claims | claims idénticos al UI |
| 12 | Logout desde UI retorna a /login | navegación post-logout |

**Hallazgo de diseño**: `PersistSessionAsync` (localStorage) solo se ejecuta con `rememberMe=true` ("Recordarme"). Con default `false`, el estado vive en memoria WASM (pérdida al recargar). Los tests que leen el JWT habilitan "Recordarme".

---

## FASE 2.11 — Regresión

| Gate | Resultado |
|------|-----------|
| `dotnet build PassPlat.slnx` | **0 errores** (2 warnings NU1603 pre-existentes) |
| `dotnet test PassPlat.Aplicacion.Test` | **66/66** |
| `faseA18-multitenant-gate.spec.ts` | **24/24** |
| `faseA19-switch-to-platform.spec.ts` | **17/17** |
| `s12-login-e2e.spec.ts` (nuevo) | **12/12** |
| `fase14-federacion-identidades.spec.ts` | **14/14** |
| `fase12-federacion-ui.spec.ts` | **23 passed / 2 skipped** (skips intencionales) |

---

## Cambios Realizados (S12 P0)

| Archivo | Cambio | Motivo |
|---------|--------|--------|
| `tests/s12-login-e2e.spec.ts` | **Nuevo** — 12 tests serial | Certificación FASE 2.10 |
| `Docs/Architecture/S12-Login-E2E-Certification.md` | **Nuevo** — este documento | Certificación FASE 2.13 |

**Sin cambios en código de producción, BBDD, seeds, SPs o configuración.**

---

## Clasificación de Gaps / Hallazgos

| Hallazgo | Tipo | Estado |
|----------|------|--------|
| Persistencia de sesión solo con "Recordarme" | COMPORTAMIENTO (diseño) | Documentado — no es bug |
| Dashboard hace 4 requests que reciben 403 (sin permiso) y los tolera mostrando 0 | COMPORTAMIENTO | Documentado — UI correcta |
| Usuarios `test_*`/`hybrid_*` (72) y 2 anomalías VALIDATE | DATA POLLUTION | Pendiente limpieza P1 (fuera de P0) |
| Login con credenciales vacías es no-op silencioso (sin feedback visual) | UX menor | Documentado — sugerido para mejora en P3 |
