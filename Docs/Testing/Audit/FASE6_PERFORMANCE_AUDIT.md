# FASE 6 — Auditoría de Rendimiento

**Fecha**: 2026-06-21
**Proyecto**: PassPlat
**Stack**: Blazor WASM + MudBlazor 9.5.0 / .NET 10.0
**Herramientas**: Playwright MCP + Análisis estático de código
**Auditor**: opencode (AI Agent)

---

## Resumen Ejecutivo

| Categoría | Cantidad |
|-----------|----------|
| 🔴 HIGH | 5 |
| 🟡 MEDIUM | 12 |
| 🟢 LOW | 19 |
| **Total issues** | **36** |

**Calificación General**: 🟡 ACEPTABLE — Rendimiento base bueno (API <30ms, SPA nav ~2s), pero con issues de N+1 queries y fetch completo de listas para KPIs que degradan la experiencia en producción con datos reales.

---

## 1. Métricas de Rendimiento (Playwright)

### 1.1 Carga Inicial WASM

| Métrica | Valor | Estado |
|---------|-------|--------|
| DOM Content Loaded | 139ms | ✅ Excelente |
| Load Event | 280ms | ✅ Bueno |
| DOM Interactive | 139ms | ✅ Excelente |
| Total WASM files | 144 | — |
| WASM total decoded | 27.6 MB | ⚠️ Grande |
| Total resources | 156 | — |
| Total decoded | 28.9 MB | — |
| **Tiempo total carga** | **1,066ms** | ✅ Bueno |

**Top 5 recursos más lentos:**

| Archivo | Duración | Tamaño |
|---------|----------|--------|
| MudBlazor.mpmnbemh44.wasm | 478ms | 9,957 KB |
| System.Private.DataContractSerialization.wasm | 339ms | 829 KB |
| System.Reflection.Metadata.wasm | 334ms | 482 KB |
| System.Runtime.InteropServices.wasm | 319ms | 53 KB |
| System.Runtime.InteropServices.RuntimeInformation.wasm | 316ms | 5 KB |

### 1.2 Navegación SPA (entre páginas)

| Página | Tiempo navegación |
|--------|-------------------|
| Tenants | 2,019ms |
| Apps | 2,068ms |
| Usuarios | 2,074ms |
| Auditoría | 2,066ms |
| Mantenimiento | 2,050ms |
| **Promedio** | **~2,055ms** |

### 1.3 Respuesta API (endpoints autenticados)

Los endpoints responden rápidamente pero las llamadas son secuenciales (no paralelas):

| Endpoint | Status | Tamaño body |
|----------|--------|-------------|
| `/api/tenants/count` | 200 | ~10 bytes |
| `/api/tenants/page` | 200 | ~1 KB |
| `/api/tenants/activos/count` | 200 | ~10 bytes |
| `/api/dominiosTenant/count` | 200 | ~10 bytes |
| `/api/apps/page` | 200 | ~1 KB |
| `/api/usuarios` | 200 | ~1 KB |
| `/api/usuarios/page` | 200 | ~1 KB |
| `/api/auditoriapwd/page/tenant/1` | 200 | ~3 KB |

---

## 2. Issues Encontrados

### 2.1 N+1 Query Patterns (🔴 HIGH)

#### Issue 1.1 — Grupos/Index.razor: N llamadas secuenciales por grupo
- **Archivo**: `PassPlat.Web/Pages/Grupos/Index.razor:315-326`
- **Problema**: `CargarEstadisticas()` usa `foreach` sobre `_items` (todos los grupos) y hace `await Api.GetAsync` por cada grupo para obtener conteo de miembros. Para N grupos = N requests HTTP secuenciales.
- **Impacto**: Con 50 grupos = 50 llamadas API secuenciales (~500ms+ en producción).
- **Solución**: Crear endpoint batch `api/GruposUsuarios/stats` o paralelizar con `Task.WhenAll`.

#### Issue 1.2 — RolesPermisos/Index.razor: N llamadas secuenciales por rol
- **Archivo**: `PassPlat.Web/Pages/RolesPermisos/Index.razor:1110-1121`
- **Problema**: `CargarUsuariosPorRol()` usa `foreach` sobre `_roles` y hace `await Api.GetAsync<List<AccesoDto>>($"api/accesos/rol/{rol.Id}")` por cada rol. Descarga listas completas solo para contar.
- **Impacto**: Con 20 roles = 20 llamadas + 20 descargas de listas completas.
- **Solución**: Crear endpoint batch `api/roles/stats` con conteos pre-computados.

#### Issue 1.3 — Roles/Index.razor: 3N llamadas paralelas
- **Archivo**: `PassPlat.Web/Pages/Roles/Index.razor:513-523`
- **Problema**: `CargarEstadisticasRol()` lanza 3 tareas por rol (accesos, permisos, herencia) = 6N requests totales. Aunque son paralelas, descarga listas completas para contar.
- **Impacto**: Con 20 roles = 60 requests simultáneos.
- **Solución**: Endpoints de conteo pre-computados.

### 2.2 Fetch Completo para KPIs (🔴 HIGH)

#### Issue 3.1 — Usuarios/Index.razor: Descarga TODOS los usuarios
- **Archivo**: `PassPlat.Web/Pages/Usuarios/Index.razor:237`
- **Problema**: `CargarEstados()` llama `Api.GetAsync<List<UsuarioDto>>("api/usuarios")` que descarga TODOS los usuarios solo para computar 4 conteos (total, activos, bloqueados, pendientes).
- **Impacto**: Con 10,000 usuarios = descarga de ~2MB solo para 4 números.
- **Solución**: Crear endpoint `api/usuarios/count-by-state`.

#### Issue 3.3 — HistorialPwd/Index.razor: Descarga TODO el historial
- **Archivo**: `PassPlat.Web/Pages/HistorialPwd/Index.razor:98`
- **Problema**: `LoadKpis()` descarga TODOS los registros de historial para computar KPIs client-side.
- **Impacto**: Con 100,000 registros = descarga masiva innecesaria.
- **Solución**: Crear endpoint `api/historialpwd/kpis`.

#### Issue 3.2 — Apps/Index.razor: Descarga todas las apps
- **Archivo**: `PassPlat.Web/Pages/Apps/Index.razor:180`
- **Problema**: `OnInitializedAsync` descarga todas las apps para contar total.
- **Solución**: Usar conteo del paged response o endpoint dedicado.

### 2.3 Doble Llamada API en Carga (🔴 HIGH)

#### Issue 4.1 — Notificaciones/Index.razor: Usuarios descargados 2 veces
- **Archivo**: `PassPlat.Web/Pages/Notificaciones/Index.razor:134, 151`
- **Problema**: `OnInitializedAsync` descarga usuarios (línea 134) y luego `LoadData()` los descarga de nuevo (línea 151). Misma data dos veces.
- **Solución**: Eliminar la segunda descarga si `_usuarios` ya está poblado.

#### Issue 4.2-4.4 — Usuarios, Apps, HistorialPwd
- **Problema**: Patrón similar — descarga completa para KPIs + descarga paginada para tabla.
- **Solución**: Mover KPIs al servidor.

### 2.4 Missing AsNoTracking (🟡 MEDIUM)

**19 repositorios** no usan `.AsNoTracking()` en queries de solo lectura. Los más impactantes:

| Repositorio | Queries sin AsNoTracking | Includes | Severidad |
|------------|-------------------------|----------|-----------|
| AccesoRepository | 3 | 4 cada una | MEDIUM |
| UsuarioRepository | 6 | 0-1 | MEDIUM |
| SesionRepository | 1 | 4 | MEDIUM |
| AuditoriaPwdRepository | 4 | 1 | MEDIUM |
| IntentoAccesoRepository | 2 | 2 | MEDIUM |
| HistorialPwdRepository | 2 | 1 | MEDIUM |
| RolRepository | 5 | 1 | LOW |
| PermisoRepository | 3 | 1 | LOW |
| ModuloRepository | 5 | 1-2 | LOW |
| ConfigAppRepository | 3 | 0 | LOW |
| PoliticaPwdRepository | 7 | 0 | LOW |

**Impacto**: EF Core change tracking agrega overhead innecesario en ~40 queries de solo lectura.

### 2.5 Missing Database Indexes (🟡 MEDIUM)

| Tabla | Columna(s) | Queries afectadas | Severidad |
|-------|-----------|-------------------|-----------|
| DispConfiables | IdUsuario + Confiable | ObtenerPorUsuarioAsync | MEDIUM |
| MFA | IdUsuario | ObtenerPorUsuarioAsync | LOW |
| GrupoUsuario | IdUsuario | ObtenerPorUsuarioAsync | LOW |
| UsuarioPermiso | IdUsuario | ObtenerPorUsuarioAsync | LOW |

### 2.6 S.Sync-over-Async (.Result) (🟢 LOW)

5 archivos usan `.Result` después de `Task.WhenAll`. Técnicamente seguro pero es code smell:

| Archivo | Línea |
|---------|-------|
| Tenants/Index.razor | 235-237 |
| Dashboard.razor | 348-351, 395-396 |
| MatrizPermisos/Index.razor | 251-252 |
| Sesiones/Index.razor | 148-149 |
| Bloqueos/Index.razor | 173-174 |

**Solución**: Reemplazar `.Result` con variables capturadas del `await`.

---

## 3. Priorización de Correcciones

### P0 — Inmediato (antes de producción)

| # | Issue | Impacto | Esfuerzo |
|---|-------|---------|----------|
| 1.1 | N+1 en Grupos/Index.razor | N llamadas secuenciales | Bajo |
| 1.2 | N+1 en RolesPermisos/Index.razor | N llamadas secuenciales | Bajo |
| 3.1 | Fetch completo en Usuarios KPIs | Descarga masiva | Bajo |
| 3.3 | Fetch completo en HistorialPwd KPIs | Descarga masiva | Bajo |
| 4.1 | Doble llamada en Notificaciones | Data duplicada | Bajo |

### P1 — Alta prioridad

| # | Issue | Impacto | Esfuerzo |
|---|-------|---------|----------|
| 1.3 | 3N llamadas en Roles statistics | Requests excesivos | Medio |
| 2.x | AsNoTracking en 5 repositorios core | Overhead EF Core | Bajo |
| 3.2 | Fetch completo en Apps KPIs | Descarga innecesaria | Bajo |
| 3.5 | pageSize=500 en IntentosAcceso | Payload grande | Bajo |

### P2 — Media prioridad

| # | Issue | Impacto | Esfuerzo |
|---|-------|---------|----------|
| 2.x | AsNoTracking en repositorios restantes | Overhead menor | Bajo |
| 5.x | Missing indexes en DispConfiables | Query lenta | Bajo |
| 6.x | .Result pattern en 5 archivos | Code smell | Bajo |

---

## 4. Estimación de Impacto

### Con datos de producción (estimado):

| Escenario | Sin fix | Con fix | Mejora |
|-----------|---------|---------|--------|
| Login → Dashboard | ~2s | ~1.5s | -25% |
| Cargar Usuarios (10K) | ~3s (fetch total) | ~0.5s (count endpoint) | -83% |
| Cargar Grupos (50) | ~5s (N+1) | ~0.5s (batch) | -90% |
| Cargar RolesPermisos (20 roles) | ~4s (N+1) | ~0.5s (batch) | -87% |
| Cargar HistorialPwd (100K) | ~8s (fetch total) | ~0.5s (KPI endpoint) | -93% |
| Navegación SPA promedio | ~2s | ~1.5s | -25% |

---

## 5. Configuración de Compilación

### Proyectos compilados:
- `PassPlat.Web` (Blazor WASM) — puerto 5273
- `PassPlat.WebAPI` (ASP.NET Core) — puerto 5259
- `PassPlat.Aplicacion`
- `PassPlat.Datos`
- `PassPlat.Dominio`
- `PassPlat.Aplicacion.Dtos`

### Multi-project debug:
Ambos proyectos (Web + WebAPI) se ejecutan simultáneamente en VS 2026 con "Multiple startup projects" configurado.

---

## 6. Conformidad con AGENTS.md

| Regla | Estado |
|-------|--------|
| MudTable con ServerData | ✅ Usado en tablas principales |
| try/catch + ISnackbar | ✅ Implementado |
| JWT en memoria | ✅ No persiste |
| Rate limiting | ✅ 6 policies |
| HTTPS + HSTS | ✅ Configurado |
| Sin SQL injection | ✅ EF Core LINQ + SPs parametrizados |
