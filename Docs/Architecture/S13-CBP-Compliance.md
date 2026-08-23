# S13 — F5: Auditoría de Adopción CBP (Framework Compliance)

- **Sprint**: S13 — Post-Login Authorization, CBP Compliance & App/Tenant Configuration
- **Fase**: F5 — Auditoría CBP read-only
- **Fecha**: 2026-08-03
- **Método**: Análisis estático (grep + Roslyn). **Sin cambios de código.**

---

## 1. Resultado Global

| Área CBP | Estado | Evidencia |
|----------|--------|-----------|
| CBP.Results (Result<T>) | ✅ ADOPTADO | 2,197 matches en Aplicacion/Datos/WebAPI |
| CBP.Data (RepositoryAsync/UoW) | ✅ ADOPTADO | 246 matches |
| CBP.Services (ServiceAsync/IServiceAsync) | ✅ ADOPTADO | 224 matches |
| CBP.Security.Cryptography (Argon2id/AES) | ✅ ADOPTADO | IPasswordService + IEncryptionService |
| CBP.Caching (ICacheService) | ✅ ADOPTADO | 6 servicios + middleware |
| CBP.Emails (IEmailQueue) | ✅ ADOPTADO | EmailQueue + EmailBackgroundService |
| Named HttpClient + resiliencia | ✅ CUMPLE | 4 named clients + AddStandardResilienceHandler |
| Sin ConcurrentDictionary/IMemoryCache/IDistributedCache en negocio | ✅ CUMPLE | 0 usos en Aplicacion |
| CBP.Events (DomainEventDispatcher) | ⚠️ PARCIAL | EventBase definidos, sin dispatcher |
| CBP.MultiTenant (ITenantContext) | ✅ ADOPTADO | Controllers + middleware |

**Conclusión**: PassPlat cumple la adopción CBP en las áreas operativas. Solo 1 hallazgo menor
(CBP.Events) y 2 observaciones de limpieza. **Sin acción correctiva obligatoria en S13.**

---

## 2. Detalle por Área

### 2.1 CBP.Results — Result<T> como patrón único
- Repositorios: todo método público async retorna `Task<Result<T>>` con try-catch `DB_ERROR`
  (ej. `AuthRepository.cs:58-62`, `UsuarioRepository`).
- Servicios: verificación `IsFailure` antes de `result.Value` (ej. `AuthService.cs:104`).
- Controladores: helpers `FromResult`/`FromResultQuery`/`CreatedFromResult` vía `BaseApiController`
  (370 matches en `PassPlat.WebAPI`).
- **Veredicto**: cumple la cadena DB→Repo→Service→Controller→UI.

### 2.2 CBP.Data — RepositoryAsync<T> + IUnitOfWorkAsync
- Todos los repositorios: `class XRepository : RepositoryAsync<T>, IXRepository` (genérico único).
- `IUnitOfWorkAsync<PassPlatDbContext>` inyectado en controllers/servicios para `SaveChangesAsync`
  (commit solo desde consumer/API).
- `AddUnitOfWorkAsync<PassPlatDbContext>()` en `Program.cs:141`.
- **Veredicto**: cumple patrón repo/UoW.

### 2.3 CBP.Services — ServiceAsync<T, TDto>
- Servicios de catálogo/contexto: `class XService : ServiceAsync<T, TDto>, IXService`
  (ej. `ConfProvIdenService`, `RolService`, `TokenRestService`).
- Servicios SP: interfaces extienden `ICustomService`.
- Registro DI: `AddServiceAsync<T, TImpl>()` (142 registros en `AplicacionDependencyInjection.cs`).
- **Veredicto**: cumple.

### 2.4 CBP.Security.Cryptography — Argon2id + AES-256
- `IPasswordService` (CBP) inyectado en `AuthService`/`PasswordService`/`PassPlatPasswordSecurity`.
- `IEncryptionService` usado para: `ClientSecret` (ConfProvIden), tokens OAuth (IdenExtTokens),
  passwords email (EmailAccount), ConfigApp encriptada.
- **Veredicto**: cumple.

### 2.5 CBP.Caching — ICacheService exclusivo
- Usos: `ExternalAuthService` (OAuth state/replay), `JwksStore`, `MfaCodeStore`, `EmailTemplateStoreService`,
  `DashboardEnterpriseService`, `TenantResolutionMiddleware`, `ExternalAuthController`.
- Registro: `AddCbpCache(cache => cache.UseLocal(new MemoryCacheProvider()))` (`Program.cs:150-153`).
- `AddMemoryCache()` en `Program.cs:149` registra `IMemoryCache` **solo como provider subyacente**
  del `MemoryCacheProvider` — no se inyecta `IMemoryCache` en código de negocio (0 matches en Aplicacion).
- **Veredicto**: cumple regla 18. `IDistributedCache` no se usa en runtime.

### 2.6 CBP.Emails — pipeline email
- `IEmailQueue`/`EmailQueue` (singleton) + `EmailBackgroundService` (hosted) + `PassPlatEmailService`
  (usa `CBP.Emails.Configuration/Core/Services`).
- 22 `EnqueueAsync(new EmailJob(...))` distribuidos en servicios de negocio.
- **Veredicto**: cumple.

### 2.7 Named HttpClient + resiliencia
- 4 named clients: `OAuth.Jwks`, `OAuth.Token`, `OAuth.UserInfo`, `OAuth.Revocation`,
  todos con `.AddStandardResilienceHandler()` (`Program.cs:154-174`).
- 7 providers OAuth inyectan `IHttpClientFactory` (no `new HttpClient()`).
- `new HttpClient(` → **0 matches** en `PassPlat.Aplicacion`.
- **Veredicto**: cumple regla 19.

### 2.8 Ausencia de anti-patrones de caché/concurrencia
- `ConcurrentDictionary` → **0** en Aplicacion/Datos.
- `IMemoryCache`/`IDistributedCache`/`MemoryCache` → **0 usos** en Aplicacion (solo registración provider en Program.cs).
- **Veredicto**: cumple.

### 2.9 CBP.Events — EventBase + DomainEventDispatcher ⚠️
- **Definidos**: `NewIpDetectedEvent`, `SecurityAlertEvent` (`IPEvents.cs`), eventos de disp
  (`DispConfiableEvents.cs`) — todos `: EventBase` (cumplen herencia CBP.Events).
- **Publicación**: `IPEventPublisher`/`DispConfiableEventPublisher` son **static** y encolan
  directamente a `IEmailQueue.EnqueueAsync` — **no usan `DomainEventDispatcher`**.
- `DomainEventDispatcher`/`DispatchAsync` → **0 matches** en Aplicacion.
- **Hallazgo (menor)**: la infraestructura de eventos CBP está disponible pero el dispatcher
  no se utiliza; los eventos se traducen directamente a jobs de email.
- **Impacto**: funcional (emails funcionan), pero no aprovecha suscripción/paralelismo/correlation
  del dispatcher. **No bloquea S13**; registrar como mejora futura.

### 2.10 CBP.MultiTenant — ITenantContext
- `TenantResolutionMiddleware` (WebAPI) resuelve el tenant actual; controllers inyectan
  `ITenantContext` (`CurrentId`); `IntentoAccesoService` lo inyecta.
- **Veredicto**: cumple.

---

## 3. Observaciones de limpieza (no bloqueantes)

1. `AplicacionDependencyInjection.cs:108-109` — comentario desactualizado que menciona
   `IDistributedCache` para OAuth state; en realidad se usa `ICacheService`. Corregir comentario.
2. `AplicacionDependencyInjection.cs` registra `IJwksStore` como singleton pero `JwksStore`
   inyecta `IHttpClientFactory` + `ICacheService` (ambos singleton-compatibles). OK.

## 4. Acciones S13

- **Ninguna corrección obligatoria**. Hallazgo 2.9 y comentario 3.1 quedan registrados como
  **deuda técnica** para sprint futuro (fuera de alcance S13 read-only).

## 5. Estado

**F5 COMPLETADO** (read-only). Documento generado.
