# S13 — Environment Baseline

> FASE 1 del Sprint S13 — Baseline canónico del entorno de ejecución.
> Fecha: 2026-08-03 · Estado: ✅ CERTIFICADO

---

## 1. Contrato de puertos (S13)

| Rol | Web | API | Certificación principal |
|---|---|---|---|
| **Canónico** | `https://localhost:7275` | `https://localhost:5001/api` | S13 certifica contra estos |
| **Auxiliar testing** | `http://localhost:5273` | `http://localhost:5259/api` | Solo perfiles legacy/auxiliares, documentados |

**Regla**: la certificación principal de S13 se ejecuta contra `https://localhost:5001/api`
y `https://localhost:7275`. `5259/5273` quedan como perfiles auxiliares **exclusivamente**
para el fixture `request` de Node (rechaza cert self-signed) y NO representan el runtime
oficial. **`http://localhost:5000` y `5273` NO son runtime oficial.**

## 2. Evidencia de servicios activos

| Servicio | PID | Puertos en escucha | Respuesta HTTPS |
|---|---|---|---|
| WebAPI (`PassPlat.WebAPI`, perfil `https`) | 27580 | `5001` (https) + `5259` (http) | `GET https://localhost:5001/api/apps/activas` → **200** |
| Web WASM (`PassPlat.Web`, perfil `https`) | 19772 | `7275` (https) + `5273` (http) | `GET https://localhost:7275/` → **200** |

- `GET https://localhost:5001/health` → **200 Healthy** (health endpoint sin prefijo `/api`).
- Swagger: **NO habilitado** (no hay `UseSwagger`/`AddSwaggerGen` en `Program.cs`).
- Arranque: `dotnet run --project PassPlat.WebAPI --launch-profile https --no-build` (igual Web).
- `ASPNETCORE_ENVIRONMENT=Development` (definido en `launchSettings.json` perfil `https`).

## 3. Cadena canónica certificada (evidencia navegador)

```
Browser
  → https://localhost:7275/login                 (200, página renderiza)
      → https://localhost:5001/api/apps/activas   (200)   [App catalogo]
      → https://localhost:5001/api/auth/tenant-info (200)
      → https://localhost:5001/api/auth/tenants    (200)
      → https://localhost:5001/api/auth/login      (200, JWT)  [login real]
  → SQL Server / PassPlat (SP_Auth_Login, SP_Permisos_Usuario_Efectivos)
```

Network del navegador (Chrome DevTools): todos los requests a la API usan
`https://localhost:5001/api/*` — **ninguno** apunta a `5259` en el runtime de la Web.

## 4. Configuración de archivos

| Archivo | Valor |
|---|---|
| `PassPlat.WebAPI/Properties/launchSettings.json` | perfil `https`: `applicationUrl=https://localhost:5001;http://localhost:5259`, `ASPNETCORE_ENVIRONMENT=Development` |
| `PassPlat.Web/Properties/launchSettings.json` | perfil `https`: `applicationUrl=https://localhost:7275;http://localhost:5273` |
| `PassPlat.Web/wwwroot/appsettings.json` | `ApiBaseUrl: "https://localhost:5001"`, `AppSettings.AppId: 1` |
| `PassPlat.WebAPI/appsettings.json` | `Jwt.Issuer=PassPlat`, `Audience=PassPlat`, `ExpirationMinutes=60`, `RefreshTokenExpirationMinutes=1440`; `Jwt.SecretKey` vacío (env) |
| `PassPlat.WebAPI/appsettings.Development.json` | `ConnectionStrings.PassPlatDb = Server=.;Database=PassPlat;User Id=sa;Password=inicio123;TrustServerCertificate=True` |

### Secretos (NO en appsettings)

| Clave | Fuente | Evidencia |
|---|---|---|
| `Jwt__SecretKey` | variable de entorno | `Jwt__SecretKey = [SET, len=43]` |
| `Encryption__Key` | variable de entorno | `Encryption__Key = [SET, len=44]` (base64, 32 bytes AES-256) |
| `ConnectionStrings__PassPlatDb` | variable de entorno | `[SET, len=85]` (sobreescribe Development) |

El API **valida** en `Program.cs:53-58`: SecretKey no vacío y >= 32 chars, y en no-Development
no puede empezar por `CHANGEME`.

## 5. Pipeline HTTP / Seguridad

| Item | Config | Línea |
|---|---|---|
| HSTS | `app.UseHsts()` — siempre | `Program.cs:232` |
| HTTPS Redirection | `UseHttpsRedirection()` **solo si no Development** | `Program.cs:233-234` |
| CORS (Development) | `http://localhost:5258`, `https://localhost:5258`, `http://localhost:5273`, `https://localhost:7275`, `https://localhost:5001` — AllowAnyMethod/Header, AllowCredentials | `Program.cs:197-206` |
| CORS (prod) | `https://app.passplatapp.com` | `Program.cs:207-209` |
| Auth | `AddCbpAuthentication(AutoChallenge=false)`, `AddJwtOperator` | `Program.cs:49-50` |
| Auth middleware | `UseCbpAuthentication()` | `Program.cs:244` |
| Rate limiter `LoginPolicy` | `PermitLimit = Development ? 100 : 5` | `Program.cs:89` |
| Health | `app.MapHealthChecks("/health")` | `Program.cs:249` |

Nota BUG-017.1.3 (cerrado en ciclo anterior): `UseHttpsRedirection` envuelto en
`if (!app.Environment.IsDevelopment())` elimina el redirect 307 que rompía `Authorization: Bearer` en desarrollo.

## 6. JWT certificado (login interno HTTPS 5001)

Request: `POST https://localhost:5001/api/auth/login` `{ NomUsuario: "test_multitenant", password: "Admin@123", idApp: 1, idTenant: 3 }` → **HTTP 200**, campos `accessToken` + `refreshToken`.

Payload decodificado (solo header/payload, sin exponer firma):

| Claim | Valor |
|---|---|
| `nameidentifier` (sub) | `8` |
| `IdApp` | `1` (PASSPLAT) |
| `TenantId` | `3` (ABARROTES) |
| `UsuarioTenantId` | `4` |
| `permiso[]` | `ACCESOS_VER, GRUPOS_VER, MATRIZ_PERMISOS_VER, PERMISOS_VER, ROLES_VER, USUARIOS_VER, USUARIOS_VERDISP` (7) |
| `iss` / `aud` | `PassPlat` / `PassPlat` |
| `iat` / `exp` | 1785806047 / 1785809647 (60 min) |

Coincide con la certificación S12: `SP_Permisos_Usuario_Efectivos(8,3,1)` → mismos 7 permisos.

## 7. Build

```
dotnet build PassPlat.slnx
→ Compilación correcta. 0 Errores, 5 Advertencias (pre-existentes)
```
Warnings pre-existentes (no introducidos por S13):
- `NU1603` ×2 — EF Core Design preview version resolution (WebAPI).
- `CS8602` ×3 — null-deref posibles: `CrearConfProvIdenValidator.cs:51,56`, `ConfProvIdenService.cs:119`.

## 8. Datos de prueba (entorno)

| Item | Valor |
|---|---|
| Usuario | `test_multitenant` (Id=8) |
| Password | `Admin@123` |
| App | PASSPLAT (Id=1) |
| Tenant | ABARROTES (Id=3) |
| UsuarioTenantId | 4 |
| Rol | ABARROTES_CONSULTA (Id=12) |
| Tenants disponibles | PLATFORM(1), ABARROTES(3), VESTUARIO(4) |
| SQL | `Server=.;Database=PassPlat`, `sa`/`inicio123` |

## 9. Riesgos / Notas

1. **Fixture `request` de Node no soporta cert self-signed** → los tests API de S13 usarán
   `playwright.request.newContext({ baseURL: "https://localhost:5001/api", ignoreHTTPSErrors: true })`.
   El fixture `request` legacy seguirá usando `http://localhost:5259/api` solo donde sea estrictamente necesario (documentado).
2. **Login real con rate limit**: en Development `LoginPolicy.PermitLimit=100`, pero si el entorno
   cambia a no-Development baja a 5 → máximo 5 reintentos para evitar 429.
3. **Swagger deshabilitado** → la evidencia de endpoints se hace vía HTTP real, no UI.
4. Los secretos residen en variables de entorno del proceso (no user secrets de máquina, no appsettings).
   No registrar valores de secretos en este documento.

## 10. Evidencia capturada

| Evidencia | Fuente |
|---|---|
| Login page renderiza (`https://localhost:7275/login`) | Snapshot Chrome DevTools (uid 1_0) |
| Requests Web→API todos `https://localhost:5001/api/*` (200) | Chrome DevTools Network (reqid 225-227) |
| Login real → HTTP 200 + JWT | Invoke-WebRequest HTTPS 5001 |
| `/health` → Healthy | Invoke-WebRequest HTTPS 5001 |
| Screenshot `s13-f1-login-canonic.png` | Chrome DevTools (Temp) |

---

## F1 — Checklist

- [x] API canónica `https://localhost:5001/api` responde 200 (apps/activas, login, health)
- [x] Web canónica `https://localhost:7275` responde 200 y renderiza login
- [x] Cadena Browser→API→DB verificada (requests reales 200)
- [x] 5259/5273 documentados como auxiliares (NO runtime oficial)
- [x] Secretos localizados (env vars) sin exponer valores
- [x] CORS, HSTS, HTTPS, rate limit, health documentados
- [x] Build 0 errores / 5 warnings pre-existentes
- [x] JWT login interno verificado (sub/IdApp/Tenant/UsuarioTenantId/perms)
- [x] Puertos 5000/5273 descartados como runtime oficial
