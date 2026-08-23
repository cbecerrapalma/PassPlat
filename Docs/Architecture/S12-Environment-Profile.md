# S12 FASE 2.1 — Perfil de Entorno

## Estado
Capturado 2026-08-02 (certificación UI login 7275).

## Procesos activos en puertos clave

| Puerto | PID | Nombre | Ruta | Rol |
|--------|-----|--------|------|-----|
| 7275 | 13492 | dotnet | `C:\Program Files\dotnet\dotnet.exe` | Web WASM (HTTPS, target) |
| 5273 | 13492 | dotnet | `C:\Program Files\dotnet\dotnet.exe` | Web WASM (HTTP, mismo proceso) |
| 5000 | 16164 | PassPlat.WebAPI | `D:\CODIGOS\PassPlat\PassPlat.WebAPI\bin\Debug\net10.0\PassPlat.WebAPI.exe` | API |

> **7275 y 5273 son el mismo proceso** (DevServer de Blazor WASM bindea ambos endpoints del perfil `https`).

## Web (`PassPlat.Web`)

| Atributo | Valor |
|----------|-------|
| URL objetivo | `https://localhost:7275` |
| URL alternativa | `http://localhost:5273` |
| Proyecto | `D:\CODIGOS\PassPlat\PassPlat.Web\PassPlat.Web.csproj` |
| Launch profile | `https` |
| `applicationUrl` (launchSettings.json) | `https://localhost:7275;http://localhost:5273` |
| PID | 13492 |
| Entorno | Development |
| `ApiBaseUrl` (wwwroot/appsettings.json) | `http://localhost:5000` |
| `AppId` (wwwroot/appsettings.json) | `1` |
| Página certificada | `/login` |

## API (`PassPlat.WebAPI`)

| Atributo | Valor |
|----------|-------|
| URL | `http://localhost:5000` |
| Proyecto | `D:\CODIGOS\PassPlat\PassPlat.WebAPI\PassPlat.WebAPI.csproj` |
| PID | 16164 |
| Entorno | Development |
| Comando de arranque | `dotnet run --no-build --urls http://localhost:5000` |
| launchSettings declarado | `https://localhost:5001;http://localhost:5259` (NO usado en este entorno) |
| Conexión BBDD | appsettings.Development.json |

> Nota: el `ApiBaseUrl` del Web (`http://localhost:5000`) manda sobre el `launchSettings` de la API,
> por eso la API se lanza con override `--urls`.

## SQL Server

| Atributo | Valor |
|----------|-------|
| Servidor | `.` (localhost) |
| Base de datos | `PassPlat` |
| Autenticación | SQL (sa) |
| Cadena | `Server=.;Database=PassPlat;User Id=sa;Password=inicio123;TrustServerCertificate=True;` |
| `PASSWORDS.sql` de referencia | `D:\CODIGOS\BBDD\PASSWORDS.sql` |

## Endpoints verificados (2026-08-02)

| Endpoint | Estado | Resultado |
|----------|--------|-----------|
| `GET http://localhost:5000/api/apps/activas` | 200 | `[{"id":1,"codigo":"PASSPLAT","nombre":"AccessPlat"}]` |
| `GET https://localhost:7275/login` | 200 | Página de login |
| `GET http://localhost:5000/api/auth/tenants` | 200 | 3 tenants |
| `GET http://localhost:5000/api/auth/externo/proveedores-login?idTenant=1` | 200 | `[GOOGLE]` |
| `GET http://localhost:5000/api/auth/externo/proveedores-login?idTenant=3` | 200 | `[]` |
| `POST http://localhost:5000/api/auth/login` (UI) | 200 | JWT (user 8, tenant 3, UT 4) |

## Notas

- `use` de Playwright config: `baseURL http://localhost:5273`, `ignoreHTTPSErrors: true`.
- La suite de contrato 2.1 usa `WEB_BASE` explícito `https://localhost:7275` (override con
  `WEB_BASE_URL`).
- BUG-017.1.3 resuelto previamente (env de Development no fuerza HTTPS redirect), lo que permite
  que el Web hable con la API por HTTP 5000 sin perder el header Authorization.
