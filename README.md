# PassPlat

**Plataforma de identidad y acceso (IAM) multi-tenant en .NET 10**

Gestión centralizada de usuarios, aplicaciones, tenants, roles, permisos y auditoría para un ecosistema de aplicaciones, con autenticación moderna, MFA, OAuth y correo transaccional. Es el proveedor de identidad de [InventaNet](https://github.com/cbecerrapalma/InventaNet) y se construye sobre el framework [CBP](https://github.com/cbecerrapalma/CBP).

> Proyecto en desarrollo: enfocado en arquitectura limpia, seguridad y multi-tenancy; aún no está en producción.

## Características

- Usuarios, aplicaciones, tenants, roles, permisos y matriz de permisos
- Catálogo de acceso por aplicación: las aplicaciones cliente publican sus módulos y permisos, y PassPlat los administra
- Autenticación: login, refresh token, bloqueo por intentos, recuperación y cambio de contraseña, sesiones y dispositivos confiables
- Políticas de contraseña: historial, expiración y medidor de fortaleza
- MFA con TOTP
- OAuth2 / OIDC con proveedores externos: Google, Microsoft, GitHub, Facebook e Instagram
- Correo transaccional con plantillas editables y cola en segundo plano
- Eventos de dominio y patrón Outbox
- Auditoría y dashboards operacional y enterprise
- Tareas en segundo plano: expiración de contraseñas y rotación de tokens externos
- `PassPlat.Client`: cliente HTTP para que otras aplicaciones consuman la API

## Multi-tenancy

- Ámbito de plataforma (`is_system`) y ámbito de tenant (`IdTenant`)
- `UsuarioTenant` (pertenencia de un usuario a tenants), selector de tenant y `ITenantContext`
- Aislamiento por `IdTenant` con claves foráneas `Restrict` e índices filtrados

## Seguridad

- JWT HS256 (`CBP.Authentication.JwtBearer`)
- Hash de contraseñas Argon2id con pepper; PBKDF2 soportado por CBP
- OAuth2 / OIDC con Authorization Code + PKCE S256, `state`/`nonce`, protección contra replay y callback del lado del servidor
- MFA local después del login externo
- AES-256 para secretos y refresh tokens guardados
- Correlación de peticiones con W3C Trace Context

## Arquitectura

```text
PassPlat.Web       (Blazor WebAssembly, MudBlazor)
        ↓ HTTP
PassPlat.WebAPI    (ASP.NET Core, JWT, middleware)
        ↓
PassPlat.Aplicacion (+ .Dtos)   (servicios, validadores, seguridad, correo)
        ↓
PassPlat.Datos     (EF Core, DbContext, repositorios)
        ↓
PassPlat.Dominio   (entidades, fábricas, reglas de dominio)
```

| Proyecto | Responsabilidad |
|---|---|
| `PassPlat.Dominio` | Entidades y reglas de dominio; no depende de Web, API ni SQL |
| `PassPlat.Datos` | EF Core, repositorios `Task<Result<T>>`, Outbox |
| `PassPlat.Aplicacion` / `.Dtos` | Servicios de aplicación, DTOs y validación |
| `PassPlat.WebAPI` | API HTTP |
| `PassPlat.Web` | Consola de administración (Blazor WASM) |
| `PassPlat.Client` | Cliente HTTP para aplicaciones consumidoras |
| `PassPlat.Consola` | Herramienta de línea de comandos |
| `*.Tests` | Pruebas de aplicación, de arquitectura (dependencias con CBP) y de UI con bUnit |

## Correo

`EmailTemplate` → `EmailJob` → `EmailQueue` (con CorrelationId) → servicio en segundo plano / Outbox → plantillas Fluid → MailKit/MimeKit → SMTP → `EmailLog`.

## Tecnologías

C# · .NET 10 · ASP.NET Core · Blazor WebAssembly · MudBlazor 9.13.0 · Entity Framework Core 10.0.13 (SQL Server) · OAuth2/OIDC · JWT · Argon2id · MailKit/MimeKit 5.0 · Fluid 2.41 · BlazorMonaco 3.5 · AutoMapper 17 · FluentValidation 12.2 · xUnit v3 · Moq · bUnit

## Compilar

Requisito: SDK de .NET 10. PassPlat referencia proyectos de CBP por ruta relativa, así que ambos repositorios van como carpetas hermanas:

```text
<carpeta>/
├── CBP/        https://github.com/cbecerrapalma/CBP
└── PassPlat/   este repositorio
```

```bash
dotnet build PassPlat.slnx
dotnet test --project PassPlat.Aplicacion.Tests/PassPlat.Aplicacion.Tests.csproj
```

Para ejecutarlo se necesita SQL Server. Los valores locales (cadena de conexión, claves JWT, credenciales SMTP y de proveedores OAuth) van en `appsettings.Development.json` o en user-secrets, que no se publican.

## Repositorios relacionados

- [CBP](https://github.com/cbecerrapalma/CBP): framework base
- [InventaNet](https://github.com/cbecerrapalma/InventaNet): inventario, ventas y tienda en línea; consume PassPlat

## Autor

**Claudio Becerra Palma** ([cbecerrapalma](https://github.com/cbecerrapalma)), desarrollador .NET / C#.

---

Este repositorio publica solo el código C# y los archivos de la solución .NET; documentación interna, scripts y datos quedan fuera.
