# FASE 10 — Propuestas de Corrección

**Fecha**: 2026-06-21
**Proyecto**: PassPlat
**Stack**: Blazor WASM + MudBlazor 9.5.0 / .NET 10.0
**Base**: FASEs 1-9 (Estructura, Funcional, MudBlazor, Visual, Seguridad, Performance, Código, Filesystem, Arquitectura)

---

## Resumen Ejecutivo

| Severidad | Cantidad | Impacto |
|-----------|----------|---------|
| 🔴 P0 — Crítico | 4 | Seguridad / Production Blocker |
| 🟠 P1 — Alto | 8 | Performance / Data Integrity |
| 🟡 P2 — Medio | 12 | Code Quality / Mantenibilidad |
| 🟢 P3 — Bajo | 15 | Cleanup / Best Practices |
| **Total** | **39** | — |

---

## P0 — CRÍTICO (Producción Blocker)

### P0.1 — JWT SecretKey Hardcoded
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.WebAPI/appsettings.json:39`
- **Problema**: `"SecretKey": "qF3zK9aP..."` hardcodeada. Production guard solo verifica prefijo "CHANGEME", pero esta key no empieza con "CHANGEME".
- **Riesgo**: Compromiso total de autenticación JWT.
- **Corrección**: Mover a User Secrets o env vars. Implementar guard real que verifique entorno.
- **Esfuerzo**: Bajo (2h)
- **Archivos**: `appsettings.json`, `Program.cs`

### P0.2 — Encryption Key Hardcoded
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.WebAPI/appsettings.json:46`
- **Problema**: `"Key": "OWmsozXd..."` hardcodeada. Sin production guard.
- **Riesgo**: Compromiso total de cifrado AES-256.
- **Corrección**: Mover a User Secrets o env vars. Agregar validación en startup.
- **Esfuerzo**: Bajo (2h)
- **Archivos**: `appsettings.json`, `Program.cs`

### P0.3 — MFA Validar AllowAnonymous
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.WebAPI/Controllers/MfaController.cs:29`
- **Problema**: `[AllowAnonymous]` en `Validar` sobreescribe `[Authorize(Policy = "USUARIOS_VERMFA")]` de clase.
- **Riesgo**: Cualquiera puede validar códigos MFA sin autenticación.
- **Corrección**: Remover `[AllowAnonymous]`. Mantener policy de clase.
- **Esfuerzo**: Bajo (10 min)
- **Archivos**: `MfaController.cs`

### P0.4 — UsuariosController.Create CC=13
- **FASE**: 7 (Código)
- **Archivo**: `PassPlat.WebAPI/Controllers/UsuariosController.cs:94`
- **Problema**: Cyclomatic complexity 13 — lógica de creación con múltiples validaciones en un solo método.
- **Riesgo**: Bugs difíciles de detectar, difícil de testear.
- **Corrección**: Extraer a `UsuarioService.CrearConPasswordAsync()` con validación separada.
- **Esfuerzo**: Medio (4h)
- **Archivos**: `UsuariosController.cs`, `UsuarioService.cs`

---

## P1 — ALTO (Performance / Data Integrity)

### P1.1 — N+1 en Grupos
- **FASE**: 6 (Performance)
- **Archivo**: `PassPlat.Web/Pages/Grupos/Index.razor:315-326`
- **Problema**: `CargarEstadisticas()` hace N llamadas secuenciales por grupo.
- **Corrección**: Crear endpoint batch `api/GruposUsuarios/stats` o paralelizar con `Task.WhenAll`.
- **Esfuerzo**: Medio (3h)

### P1.2 — N+1 en RolesPermisos
- **FASE**: 6 (Performance)
- **Archivo**: `PassPlat.Web/Pages/RolesPermisos/Index.razor:1110-1121`
- **Problema**: `CargarUsuariosPorRol()` hace N llamadas secuenciales por rol.
- **Corrección**: Crear endpoint batch `api/roles/stats`.
- **Esfuerzo**: Medio (3h)

### P1.3 — Fetch Completo en Usuarios KPIs
- **FASE**: 6 (Performance)
- **Archivo**: `PassPlat.Web/Pages/Usuarios/Index.razor:237`
- **Problema**: Descarga TODOS los usuarios para contar 4 estados.
- **Corrección**: Crear endpoint `api/usuarios/count-by-state`.
- **Esfuerzo**: Bajo (2h)

### P1.4 — Fetch Completo en HistorialPwd KPIs
- **FASE**: 6 (Performance)
- **Archivo**: `PassPlat.Web/Pages/HistorialPwd/Index.razor:98`
- **Problema**: Descarga TODO el historial para KPIs.
- **Corrección**: Crear endpoint `api/historialpwd/kpis`.
- **Esfuerzo**: Bajo (2h)

### P1.5 — Doble Llamada en Notificaciones
- **FASE**: 6 (Performance)
- **Archivo**: `PassPlat.Web/Pages/Notificaciones/Index.razor:134, 151`
- **Problema**: Usuarios descargados 2 veces.
- **Corrección**: Eliminar segunda descarga si `_usuarios` ya poblado.
- **Esfuerzo**: Bajo (1h)

### P1.6 — Missing AsNoTracking en 5 Repos Core
- **FASE**: 6 (Performance)
- **Archivos**: `AccesoRepository`, `UsuarioRepository`, `SesionRepository`, `AuditoriaPwdRepository`, `IntentoAccesoRepository`
- **Problema**: ~40 queries sin `.AsNoTracking()`.
- **Corrección**: Agregar `.AsNoTracking()` a queries de solo lectura.
- **Esfuerzo**: Bajo (2h)

### P1.7 — HistorialPwdController.GetById sin Tenant Isolation
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.WebAPI/Controllers/HistorialPwdController.cs`
- **Problema**: Endpoint retorna datos de cualquier tenant.
- **Corrección**: Filtrar por `_tenantContext.CurrentId`.
- **Esfuerzo**: Bajo (1h)

### P1.8 — IntentosAccesoController sin Tenant Isolation
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.WebAPI/Controllers/IntentosAccesoController.cs`
- **Problema**: `ContarFallidos` no filtra por tenant.
- **Corrección**: Agregar filtro de tenant.
- **Esfuerzo**: Bajo (1h)

---

## P2 — MEDIO (Code Quality / Mantenibilidad)

### P2.1 — Email Subsystem Completo sin Uso
- **FASE**: 8 (Filesystem)
- **Archivos**: 8 archivos en `Services/Email/`
- **Problema**: 12 símbolos + 3 DI registrations sin consumo.
- **Corrección**: Eliminar o marcar como `#if FEATURE_EMAIL` para módulo futuro.
- **Esfuerzo**: Medio (3h)

### P2.2 — 20 Controllers Catálogo sin UI
- **FASE**: 8 (Filesystem)
- **Archivos**: `EstadosMFAController`, `EstadosUsrController`, `ResultadosAccesoController`, etc.
- **Problema**: Controllers que existen pero no son consumidos por el UI.
- **Corrección**: Eliminar o mover a proyecto `PassPlat.WebAPI.Catalogs`.
- **Esfuerzo**: Medio (4h)

### P2.3 — 14 Repositories Catálogo sin UI
- **FASE**: 8 (Filesystem)
- **Archivos**: `TipoModuloRepository`, `TipoAuditoriaRepository`, etc.
- **Problema**: Repositories sin consumers.
- **Corrección**: Eliminar junto con controllers.
- **Esfuerzo**: Bajo (2h)

### P2.4 — HasCheckConstraint Obsoleto
- **FASE**: 7 (Código)
- **Archivos**: `UsuarioPermisoConfiguration.cs:58`, `EmailLogConfiguration.cs:55`
- **Problema**: EF Core 10 deprecó `HasCheckConstraint`.
- **Corrección**: Cambiar a `ToTable(t => t.HasCheckConstraint())`.
- **Esfuerzo**: Bajo (1h)

### P2.5 — Duplicate Using Directive
- **FASE**: 7 (Código)
- **Archivo**: `TenantEmailAccountService.cs:9`
- **Problema**: `using PassPlat.Datos` duplicado.
- **Corrección**: Remover duplicado.
- **Esfuerzo**: Bajo (5 min)

### P2.6 — DefaultTimeout Field Sin Uso
- **FASE**: 8 (Filesystem)
- **Archivo**: `PassPlat.Web/Services/ApiClient.cs:20`
- **Problema**: `DefaultTimeout` field declarado pero no usado.
- **Corrección**: Eliminar.
- **Esfuerzo**: Bajo (5 min)

### P2.7 — LocalDateTimeConverter Huérfano
- **FASE**: 8 (Filesystem)
- **Archivo**: `PassPlat.Web/Helpers/LocalDateTimeConverter.cs`
- **Problema**: Class sin referencias.
- **Corrección**: Verificar y eliminar si no se usa.
- **Esfuerzo**: Bajo (15 min)

### P2.8 — CustomAuthenticationStateProvider Verificar
- **FASE**: 8 (Filesystem)
- **Archivo**: `PassPlat.Web/Services/CustomAuthenticationStateProvider.cs`
- **Problema**: Puede estar obsoleto si se usa JWT directo.
- **Corrección**: Verificar y eliminar si no se usa.
- **Esfuerzo**: Bajo (15 min)

### P2.9 — 5 DTOs sin Uso en Dtos.cs
- **FASE**: 8 (Filesystem)
- **Archivo**: `PassPlat.Web/Models/Dtos.cs`
- **Problema**: `CrearDispDto`, `CambiarPasswordDto`, `ValidarPasswordDto`, `ValidarMfaRequest`, `PurgeRequest`.
- **Corrección**: Eliminar si no hay UI que los use.
- **Esfuerzo**: Bajo (15 min)

### P2.10 — Synchronous-over-Async en HistorialPwdController
- **FASE**: 6 (Performance)
- **Archivo**: `PassPlat.WebAPI/Controllers/HistorialPwdController.cs:64`
- **Problema**: `.Result` en Task.
- **Corrección**: Cambiar a `await`.
- **Esfuerzo**: Bajo (15 min)

### P2.11 — UsuarioService.NotificarBienvenidaAsync CC=9
- **FASE**: 7 (Código)
- **Archivo**: `PassPlat.Aplicacion/Services/BBDD/UsuarioService.cs:133`
- **Problema**: Cyclomatic complexity 9.
- **Corrección**: Extraer lógica de email a servicio dedicado.
- **Esfuerzo**: Bajo (2h)

### P2.12 — AuthController.RestablecerPassword CC=8
- **FASE**: 7 (Código)
- **Archivo**: `PassPlat.WebAPI/Controllers/AuthController.cs:167`
- **Problema**: Cyclomatic complexity 8.
- **Corrección**: Extraer validaciones a servicio.
- **Esfuerzo**: Bajo (2h)

---

## P3 — BAJO (Cleanup / Best Practices)

### P3.1 — Enums sin Uso
- **FASE**: 7 (Código)
- **Archivos**: `EEstadoUsuario.cs`, `ETipoBloqueo.cs`, `ETipoDisp.cs`
- **Corrección**: Eliminar si no se usan en código C#.
- **Esfuerzo**: Bajo (15 min)

### P3.2 — AppSettings.AppId Property
- **FASE**: 8 (Filesystem)
- **Archivo**: `PassPlat.Web/Models/AppSettings.cs:4`
- **Corrección**: Eliminar property sin uso.
- **Esfuerzo**: Bajo (5 min)

### P3.3 — Error Leaking en AuthController
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.WebAPI/Controllers/AuthController.cs`
- **Problema**: `ex.Message` expuesta al cliente.
- **Corrección**: Retornar error genérico, logear detalle.
- **Esfuerzo**: Bajo (1h)

### P3.4 — Error Leaking en UsuariosController
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.WebAPI/Controllers/UsuariosController.cs`
- **Problema**: `DbUpdateException` expuesta al cliente.
- **Corrección**: Retornar error genérico.
- **Esfuerzo**: Bajo (1h)

### P3.5 — User Enumeration en GetByEmail
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.WebAPI/Controllers/UsuariosController.cs:78`
- **Problema**: Retorna 200 con null vs 404.
- **Corrección**: Siempre retornar 200 con resultado vacío.
- **Esfuerzo**: Bajo (15 min)

### P3.6 — ConfigAppService Logs Ciphertext
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.Aplicacion/Services/BBDD/ConfigAppService.cs`
- **Problema**: Logging de prefijo de ciphertext.
- **Corrección**: Remover o enmascarar logging.
- **Esfuerzo**: Bajo (15 min)

### P3.7 — ConfigAppDto Exposes Ciphertext
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.Aplicacion.Dtos`
- **Problema**: DTO retorna valores cifrados al cliente.
- **Corrección**: Usar DTO separado sin valores cifrados.
- **Esfuerzo**: Bajo (1h)

### P3.8 — Connection String con SA Account
- **FASE**: 5 (Seguridad)
- **Archivo**: `PassPlat.WebAPI/appsettings.json`
- **Problema**: `User Id=sa;Password=inicio123`.
- **Corrección**: Usar servicio de SQL con permisos mínimos.
- **Esfuerzo**: Medio (2h)

### P3.9 — CBP Framework Warnings (19)
- **FASE**: 7 (Código)
- **Archivos**: `CBP.Emails`, `CBP.Security.Cryptography`
- **Problema**: Nullable warnings, obsolete API usage.
- **Corrección**: Reportar al equipo CBP.
- **Esfuerzo**: N/A (framework externo)

### P3.10 — PageHeader en 11 Páginas sin Refresh
- **FASE**: 3 (MudBlazor)
- **Problema**: `PageHeader` sin `OnRefresh`.
- **Corrección**: Agregar refresh o remover parámetro.
- **Esfuerzo**: Bajo (1h)

### P3.11 — MudSelect sin Validation
- **FASE**: 3 (MudBlazor)
- **Problema**: 6 instancias sin `Required`.
- **Corrección**: Agregar `Required="true"`.
- **Esfuerzo**: Bajo (30 min)

### P3.12 — AppSettings.Auditoria sin Validation
- **FASE**: 3 (MudBlazor)
- **Problema**: ConfigAppDialog sin validación.
- **Corrección**: Agregar `Required`.
- **Esfuerzo**: Bajo (15 min)

### P3.13 — Missing Null Check en 3 Páginas
- **FASE**: 3 (MudBlazor)
- **Problema**: Accesos, ConfigApp, IntentosAcceso sin null check.
- **Corrección**: Agregar verificación.
- **Esfuerzo**: Bajo (30 min)

### P3.14 — Desktop Horizontal Scroll
- **FASE**: 4 (Visual)
- **Problema**: `02-apps`, `07-accesos` en mobile 375px.
- **Corrección**: Responsive overflow hidden o scroll horizontal.
- **Esfuerzo**: Bajo (1h)

---

## Estimación de Esfuerzo Total

| Severidad | Issues | Esfuerzo Total |
|-----------|--------|----------------|
| 🔴 P0 | 4 | ~10h |
| 🟠 P1 | 8 | ~15h |
| 🟡 P2 | 12 | ~14h |
| 🟢 P3 | 15 | ~8h |
| **Total** | **39** | **~47h** |

---

## Orden de Ejecución Recomendado

### Sprint 1 (Seguridad — Antes de Producción)
1. P0.1 — Mover JWT Key a User Secrets
2. P0.2 — Mover Encryption Key a User Secrets
3. P0.3 — Remover `[AllowAnonymous]` de MFA
4. P1.7 — Tenant isolation en HistorialPwd
5. P1.8 — Tenant isolation en IntentosAcceso
6. P3.3 — Error leaking en AuthController
7. P3.4 — Error leaking en UsuariosController
8. P3.5 — User enumeration fix

### Sprint 2 (Performance)
9. P1.1 — Fix N+1 en Grupos
10. P1.2 — Fix N+1 en RolesPermisos
11. P1.3 — Usuarios count endpoint
12. P1.4 — HistorialPwd KPIs endpoint
13. P1.5 — Fix doble llamada Notificaciones
14. P1.6 — AsNoTracking en repos core

### Sprint 3 (Code Quality)
15. P0.4 — Refactor UsuariosController.Create
16. P2.1 — Limpiar Email subsystem
17. P2.2 — Limpiar controllers catálogo
18. P2.3 — Limpiar repositories catálogo
19. P2.4 — Fix HasCheckConstraint obsoleto
20. P2.5 — Fix duplicate using
21. P2.10 — Fix sync-over-async
22. P2.11 — Refactor NotificarBienvenidaAsync
23. P2.12 — Refactor RestablecerPassword

### Sprint 4 (Cleanup)
24. P3.1 — Eliminar enums sin uso
25. P3.2 — Eliminar AppId property
26. P3.6 — Fix logging ciphertext
27. P3.7 — Fix ConfigAppDto
28. P3.8 — Cambiar connection string SA
29. P3.10 — Fix PageHeader refresh
30. P3.11 — Fix MudSelect validation
31. P3.12 — Fix ConfigAppDialog validation
32. P3.13 — Fix null checks
33. P3.14 — Fix mobile horizontal scroll
34. P2.6 — Eliminar DefaultTimeout
35. P2.7 — Eliminar LocalDateTimeConverter
36. P2.8 — Verificar CustomAuthenticationStateProvider
37. P2.9 — Eliminar DTOs sin uso
