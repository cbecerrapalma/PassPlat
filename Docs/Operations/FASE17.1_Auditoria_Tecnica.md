# Auditoría Técnica — FASE 17.1 OAuth Hardening

**Fecha**: 2026-07-19
**Auditor**: Arquitecto Senior .NET Enterprise
**Scope**: Todo el subsistema OAuth + repercusiones en PassPlat
**Score Global**: **67/100** ⚠️ — Requiere correcciones críticas antes de producción

---

## Resumen Ejecutivo

La FASE 17.1 implementó correctamente el almacenamiento persistente de tokens OAuth (IdenExtTokens), el job de rotación automática, la migración SQL transaccional y la limpieza de stores legacy. Sin embargo, introduce una **violación crítica de la política de caching** (uso directo de `IDistributedCache` y `IMemoryCache` en lugar de `CBP.Caching.*`), además de varios hallazgos de seguridad y arquitectura que deben remediarse antes de considerar el subsistema listo para producción.

| Métrica | Valor |
|---------|-------|
| Score global | 67/100 |
| Riesgos críticos | 1 |
| Riesgos altos | 3 |
| Riesgos medios | 5 |
| Riesgos bajos | 4 |
| Archivos auditados | 735 (27 proyectos) |

---

## Evaluación por Categoría

| Categoría | Puntaje | Estado |
|-----------|---------|--------|
| Arquitectura | 72/100 | ⚠️ |
| OAuth2 | 75/100 | ⚠️ |
| Seguridad | 60/100 | ⚠️ |
| SQL Server | 85/100 | ✅ |
| Repository | 80/100 | ✅ |
| UnitOfWork | 78/100 | ⚠️ |
| Blazor | 75/100 | ⚠️ |
| WebAPI | 65/100 | ⚠️ |
| Dashboard | 70/100 | ⚠️ |
| Email | 85/100 | ✅ |
| MFA | 70/100 | ⚠️ |
| **Caching (CBP.Caching)** | **20/100** | ❌ |
| Documentación | 55/100 | ⚠️ |

---

## Hallazgos Detallados

### 🔴 C1 (CRÍTICO) — Violación política CBP.Caching. Seis puntos de uso directo de IMemoryCache/IDistributedCache

**Severidad**: CRÍTICO
**Riesgo**: Bloqueante para producción. Impide migrar a Redis multi-instancia sin refactorizar 5 componentes. La política exige exclusivamente `CBP.Caching.*` via `ICacheService`.
**Archivos afectados**:

| Archivo | Cache actual | Debería usar |
|---------|-------------|--------------|
| `ExternalAuthService.cs:50,66` | `IDistributedCache` | `ICacheService` |
| `ExternalAuthController.cs:21,27` | `IDistributedCache` | `ICacheService` |
| `MfaCodeStore.cs:23,26` | `IMemoryCache` | `ICacheService` |
| `DashboardEnterpriseService.cs:52,67` | `IMemoryCache` | `ICacheService` |
| `EmailTemplateStoreService.cs:19,25` | `IMemoryCache` | `ICacheService` |
| `ConfigAppRepository.cs:26,28` | `IMemoryCache` | `ICacheService` |
| `Program.cs:134-135` | `AddMemoryCache()` + `AddDistributedMemoryCache()` | Solo `AddMemoryCache()` (necesario internamente por `MemoryCacheProvider`) |

**Impacto**:
- `IDistributedCache` (introducido en H6) no escala a Redis sin cambiar código
- `IMemoryCache` en MfaCodeStore impide compartir estado MFA entre instancias
- `IMemoryCache` en Dashboard impide invalidación distribuida de KPIs
- `IMemoryCache` en ConfigAppRepository causa incoherencia en despliegues multi-instancia
- `IMemoryCache` en EmailTemplateStoreService impide actualización centralizada de templates

**Solución recomendada**:
1. Reemplazar `IDistributedCache` en `ExternalAuthService` y `ExternalAuthController` por `ICacheService` (ya registrado en DI via `AddCbpCache`)
2. Reemplazar `IMemoryCache` en `MfaCodeStore` por `ICacheService`
3. Reemplazar `IMemoryCache` en `DashboardEnterpriseService` por `ICacheService`
4. Reemplazar `IMemoryCache` en `EmailTemplateStoreService` por `ICacheService`
5. Reemplazar `IMemoryCache` en `ConfigAppRepository` por `ICacheService`
6. Eliminar `builder.Services.AddDistributedMemoryCache()` de `Program.cs`
7. Mantener `AddMemoryCache()` porque `MemoryCacheProvider` lo necesita internamente

**Prioridad**: INMEDIATA

---

### 🟠 A1 (ALTO) — SP_Auth_RenovarTokenProveedor no utilizado por IdenExtTokensRotacionJob

**Severidad**: ALTO
**Riesgo**: El BackgroundService de rotación (H3) ejecuta lógica C# con múltiples operaciones EF Core sin transacción explícita ni control de concurrencia. En despliegue multi-instancia, dos instancias pueden procesar el mismo token simultáneamente, causando duplicados o pérdida de versión. El SP `SP_Auth_RenovarTokenProveedor` ya implementa la lógica correcta con transacción, RowVersion y auditoría integrada.
**Archivos afectados**:
- `IdenExtTokensRotacionJob.cs` — no utiliza el SP
- `Migrations/FASE17.1_Hardening_OAuth.sql:92-176` — SP_Auth_RenovarTokenProveedor creado pero no llamado

**Solución recomendada**: Modificar `IdenExtTokensRotacionJob.ProcesarTokensVencidosAsync` para invocar `SP_Auth_RenovarTokenProveedor` via `IRawQueryRepositoryAsync.ExecuteSPRawAsync()` en lugar de EF Core. Esto garantiza atomicidad y control de concurrencia en multi-instancia.

**Prioridad**: ALTA

---

### 🟠 A2 (ALTO) — RedirectUri dinámico inseguro en Callback

**Severidad**: ALTO
**Riesgo**: El controller construye `redirectUri` dinámicamente con `Request.Host` + `Request.Scheme` cuando `session.RedirectUri` es null (línea 100-101). Esto viola la regla #4 de AGENTS.md: "RedirectUri debe provenir exclusivamente de ConfProvIden.Callback". Adicionalmente, no hay validación contra whitelist de URLs permitidas (open redirect).
**Archivos afectados**:
- `ExternalAuthController.cs:100-101`

**Código vulnerable**:
```csharp
var redirectUri = session.RedirectUri
    ?? $"{Request.Scheme}://{Request.Host}/api/auth/externo/{provider}/callback";
```

**Solución recomendada**: Eliminar el fallback dinámico y retornar error si `session.RedirectUri` es null. Agregar validación de RedirectUri contra la BD para prevenir open redirect.

**Prioridad**: ALTA

---

### 🟠 A3 (ALTO) — Fallo silencioso en persistencia de tokens OAuth

**Severidad**: ALTO
**Riesgo**: Cuando `PersistirTokensProveedorAsync` falla (línea 187-189), el login externo continúa exitosamente sin tokens persistidos. El usuario puede iniciar sesión pero el refresh token del proveedor se pierde para siempre, forzando una nueva autorización completa.
**Archivos afectados**:
- `ExternalAuthService.cs:186-189`

**Código**:
```csharp
var persistenceResult = await PersistirTokensProveedorAsync(...);
if (persistenceResult.IsFailure)
    _logger.LogWarning("No se pudieron persistir tokens...");
```

**Solución recomendada**: Promover a error log y opcionalmente fallar el login si no se pueden persistir tokens (configurable por proveedor). Implementar reintento con backoff.

**Prioridad**: ALTA

---

### 🟡 M1 (MEDIO) — Sin endpoint de revocación explícita de tokens OAuth

**Severidad**: MEDIO
**Riesgo**: No hay endpoint público para revocar tokens IdenExtTokens (logout OAuth). Aunque `IdenExtService.DesvincularAsync` existe como desvinculación completa, no hay un "OAuth logout" ligero que solo revoque tokens activos sin eliminar la identidad.
**Archivos afectados**:
- `IdenExtTokensRepository.cs:39-57` — `RevocarTokensAsync` existe pero no tiene controller
- `ExternalAuthController.cs` — sin endpoint `POST revoke`

**Solución recomendada**: Agregar endpoint `POST /api/auth/externo/revoke` que reciba token hint y revoque los tokens activos.

**Prioridad**: MEDIA

---

### 🟡 M2 (MEDIO) — ICacheService no inyectado correctamente en MfaCodeStore

**Severidad**: MEDIO
**Riesgo**: `MfaCodeStore` usa `IMemoryCache` directamente con TTL fijo de 5 minutos. En producción con Redis, los códigos MFA no serían visibles entre instancias. AGENTS.md documenta que MfaCodeStore usa IMemoryCache y "se pierde al reiniciar API".
**Archivos afectados**:
- `MfaCodeStore.cs`
- `Program.cs:134` — `AddMemoryCache()` necesario para MemoryCacheProvider pero MfaCodeStore lo usa directamente

**Solución recomendada**: Migrar MfaCodeStore a `ICacheService` con el mismo TTL. Esto permite que en producción con Redis los códigos MFA sean compartidos entre instancias.

**Prioridad**: MEDIA

---

### 🟡 M3 (MEDIO) — DashboardEnterpriseService inyecta IMemoryCache directamente

**Severidad**: MEDIO
**Riesgo**: El dashboard de KPIs OAuth usa `IMemoryCache` con TTL de 5 minutos. En multi-instancia, cada servidor tiene su propia caché de KPIs, causando inconsistencias visuales.
**Archivos afectados**:
- `DashboardEnterpriseService.cs:52`

**Solución recomendada**: Migrar a `ICacheService` con la misma estrategia de TTL.

**Prioridad**: MEDIA

---

### 🟡 M4 (MEDIO) — Sin validación de Nonce en callback OAuth para proveedores no-OIDC

**Severidad**: MEDIO
**Riesgo**: El nonce se genera en `GenerateAuthorizationUrlAsync` y se almacena en session store, pero en `LoginExternoAsync` solo Google valida el nonce del id_token. Los proveedores GitHub, LinkedIn, Instagram, Facebook no verifican nonce porque no emiten id_token JWT.
**Archivos afectados**:
- `IExternalIdentityProvider.cs` — interfaz no expone nonce validation explícitamente
- Providers GitHub, LinkedIn, Instagram, Facebook — no verifican nonce

**Solución recomendada**: Documentar que nonce solo aplica a proveedores OIDC. Agregar propiedad `bool SupportsNonce` en la interfaz.

**Prioridad**: MEDIA

---

### 🟡 M5 (MEDIO) — AplicacionDependencyInjection comentario engañoso sobre cache

**Severidad**: MEDIO
**Riesgo**: El comentario en línea 98-99 dice "Registrado en Program.cs via AddDistributedMemoryCache()" pero la intención correcta debería ser via ICacheService/CBP.Caching. Genera confusión de mantenimiento.
**Archivos afectados**:
- `AplicacionDependencyInjection.cs:98-99`

**Solución recomendada**: Actualizar el comentario para reflejar el uso correcto de `ICacheService`.

**Prioridad**: BAJA

---

### 🟢 B1 (BAJO) — ConfigAppRepository cachea con IMemoryCache

**Severidad**: BAJO
**Riesgo**: Bajo impacto porque ConfigApp cambia poco. Pero en multi-instancia puede dar datos inconsistentes entre servidores.
**Archivos afectados**:
- `ConfigAppRepository.cs:26`

**Solución recomendada**: Migrar a `ICacheService` cuando se implemente Redis.

**Prioridad**: BAJA

---

### 🟢 B2 (BAJO) — EmailTemplateStoreService cachea con IMemoryCache

**Severidad**: BAJO
**Riesgo**: Similar a ConfigApp — los templates cambian poco. Inconsistencia temporal aceptable.
**Archivos afectados**:
- `EmailTemplateStoreService.cs:19`

**Solución recomendada**: Migrar a `ICacheService` cuando se implemente Redis.

**Prioridad**: BAJA

---

### 🟢 B3 (BAJO) — Sin endpoints de health check para IdenExtTokensRotacionJob

**Severidad**: BAJO
**Riesgo**: No hay forma de monitorear si el BackgroundService de rotación está funcionando sin revisar logs.
**Solución recomendada**: Agregar health check expuesto via `IBackgroundStatusService` (ya existente en el proyecto).

**Prioridad**: BAJA

---

### 🟢 B4 (BAJO) — Sin prueba unitaria de SP_Auth_RenovarTokenProveedor

**Severidad**: BAJO
**Riesgo**: Si se decide usar el SP en lugar de la lógica C#, no hay test que valide su comportamiento.
**Solución recomendada**: Agregar test de integración del SP cuando se implemente el cambio de A1.

**Prioridad**: BAJA

---

## Plan de Corrección

### Correcciones Críticas (Pre-producción)

| # | Hallazgo | Acción | Archivos | Esfuerzo |
|---|----------|--------|----------|----------|
| C1a | Migrar ExternalAuthService IDistributedCache → ICacheService | Reemplazar inyección y usos | `ExternalAuthService.cs` | 1h |
| C1b | Migrar ExternalAuthController IDistributedCache → ICacheService | Reemplazar inyección y usos | `ExternalAuthController.cs` | 30min |
| C1c | Migrar MfaCodeStore IMemoryCache → ICacheService | Reemplazar implementación | `MfaCodeStore.cs` | 1h |
| C1d | Migrar DashboardEnterpriseService IMemoryCache → ICacheService | Reemplazar inyección | `DashboardEnterpriseService.cs` | 30min |
| C1e | Migrar EmailTemplateStoreService IMemoryCache → ICacheService | Reemplazar inyección | `EmailTemplateStoreService.cs` | 30min |
| C1f | Migrar ConfigAppRepository IMemoryCache → ICacheService | Reemplazar inyección | `ConfigAppRepository.cs` | 30min |
| C1g | Eliminar AddDistributedMemoryCache() de Program.cs | Una línea | `Program.cs:135` | 5min |
| C1h | Agregar AddMemoryCache() si no existe (MemoryCacheProvider lo necesita) | Una línea | `Program.cs:134` | 5min |

### Correcciones Altas (Pre-producción)

| # | Hallazgo | Acción | Archivos | Esfuerzo |
|---|----------|--------|----------|----------|
| A1 | Usar SP_Auth_RenovarTokenProveedor en RotacionJob | Modificar job para llamar SP vía IRawQueryRepositoryAsync | `IdenExtTokensRotacionJob.cs` | 2h |
| A2 | Eliminar RedirectUri dinámico en Callback | Validar contra BD, eliminar fallback Request.Host | `ExternalAuthController.cs:100-101` | 30min |
| A3 | Promover fallo de persistencia de tokens | Hacer configurable, log como error, opcionalmente fallar login | `ExternalAuthService.cs:186-189` | 1h |

### Correcciones Medias (Sprint siguiente)

| # | Hallazgo | Acción | Esfuerzo |
|---|----------|--------|----------|
| M1 | Endpoint POST revoke para tokens OAuth | Nuevo endpoint en ExternalAuthController | 2h |
| M4 | Documentar soporte Nonce por proveedor | Agregar SupportsNonce a interfaz | 1h |

### Correcciones Bajas (Backlog)

| # | Hallazgo | Esfuerzo |
|---|----------|----------|
| B1, B2, B3, B4 | Migraciones menores y tests | 2h total |

---

## Estado por Componente Arquitectura

### ✅ Clean Architecture
- Proyectos `Dominio → Datos → Aplicacion → WebAPI/Web` respetan la direccion de dependencia
- `IdenExtTokens` en Dominio, `Configuration` en Datos, `Service` en Aplicacion, `Controller` en WebAPI
- CBP.Caching es framework transversal (correcto)

### ⚠️ SOLID
- **SRP**: `ExternalAuthService` con 9 dependencias inyectadas — borderline, pero justificable por ser orquestador del flujo OAuth completo
- **OCP**: Provider Factory via `IEnumerable<IExternalIdentityProvider>` + DI — correcto
- **ISP**: `IExternalIdentityProvider` con 4 métodos (ValidateAndExtractClaims, GenerateAuthorizationUrl, RefreshTokenAsync, SupportsRefreshToken) — aceptable
- **DIP**: Todas las dependencias son interfaces — correcto
- **DRY**: Lógica de encriptación de tokens duplicada entre `ExternalAuthService.PersistirTokensProveedorAsync` y `IdenExtTokensRotacionJob` (ambos encriptan/desencriptan con el mismo patrón)

### ⚠️ OAuth2 Flow Completo

| Componente | Estado | Notas |
|-----------|--------|-------|
| Authorization Code Flow | ✅ | Correcto |
| PKCE S256 | ✅ | Generado en GenerateAuthorizationUrlAsync |
| State | ✅ | 16 bytes random, hex |
| Nonce | ✅ | 16 bytes random, base64url |
| Replay Protection | ⚠️ | Migrado a IDistributedCache (debe ser ICacheService) |
| Access Token storage | ✅ | IdenExtTokens con cifrado AES-256-GCM |
| Refresh Token storage | ✅ | IdenExtTokens con cifrado AES-256-GCM |
| Id Token storage | ✅ | IdenExtTokens |
| JWKS validation | ✅ | Google únicamente |
| Issuer validation | ✅ | Google únicamente |
| Audience validation | ✅ | Google únicamente |
| ClockSkew | ✅ | 5 min |
| Offline Access | ✅ | Google: access_type=offline&prompt=consent |
| Token Rotation Job | ⚠️ | Usa EF Core en vez de SP transaccional |
| Revocation | ❌ | Sin endpoint público |
| OAuth Logout | ❌ | Sin endpoint dedicado |

---

## Conclusión

La FASE 17.1 sienta bases sólidas: IdenExtTokens como almacén persistente de tokens, rotación automática, migración SQL transaccional y limpieza de código legacy. Sin embargo, la **violación de la política CBP.Caching** es bloqueante para producción y debe corregirse antes de cualquier despliegue multi-instancia. Las correcciones C1 (caching) y A1-A3 (SP transaccional, RedirectUri seguro, persistencia no silenciosa) deben completarse antes de certificar Google en FASE 17.2.

**Score actual: 67/100 ⚠️ — No apto para producción sin correcciones.**
**Score esperado post-correcciones: 88/100 ✅**
