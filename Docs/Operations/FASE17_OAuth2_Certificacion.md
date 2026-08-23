# FASE 17 — OAuth2 Certification & HTTPS Hardening

## Resumen

Certificación completa del subsistema OAuth2 existente y normalización de HTTPS en 15 fases. Se auditaron y reforzaron todos los componentes: seguridad, almacenamiento, escalabilidad, documentación y experiencia de usuario.

---

## Fases Completadas

### FASE 17.1 — Normalización HTTPS
- WebAPI: perfil HTTPS en `launchSettings.json` (`https://localhost:5001;http://localhost:5259`)
- HSTS habilitado en todos los entornos (`Program.cs`)
- Blazor Web `ApiBaseUrl` actualizado a `https://localhost:5001`
- CORS actualizado con orígenes HTTPS (`https://localhost:7275`, `https://localhost:5001`)

### FASE 17.2 — Reglas OAuth2 en AGENTS.md
- 17 reglas permanentes documentadas: HTTPS obligatorio, PKCE, callback único, RedirectUri desde BD, secretos cifrados, almacenamiento persistente, Provider Factory por DI, State+Nonce, Replay protection, validación IdToken, auditoría, notificaciones, MFA post-OAuth

### FASE 17.3 — Eliminación de Callback Legacy
- `SigninGoogle.razor` eliminado (sin referencias externas)
- Único flujo OAuth: Blazor → API authorize → Proveedor → API callback → JWT → Blazor `/signin-callback`

### FASE 17.4 — RedirectUri desde Base de Datos
- `ExternalAuthController.Callback()` ahora usa `session.RedirectUri` (proveniente de `ConfProvIden.Callback`)
- Eliminada construcción dinámica via `$"{Request.Scheme}://{Request.Host}/..."` que causaba mismatch en token exchange
- Validación de host eliminada (innecesaria al usar el valor exacto de BD)

### FASE 17.5 — Google Cloud Console (documentación)
- Authorized JavaScript Origins: `https://localhost:7275`
- Authorized Redirect URI: `https://localhost:5001/api/auth/externo/GOOGLE/callback` (anteriormente `http://localhost:5259/...`)
- **CRITICAL**: Actualizar Redirect URI en Google Cloud Console tras migrar a HTTPS

### FASE 17.6 — Offline Access
- `access_type=offline&prompt=consent` agregado a `GoogleIdentityProvider.GenerateAuthorizationUrlAsync()`
- Google ahora retorna RefreshToken (requerido para renovación silenciosa)

### FASE 17.7 — Tabla IdenExtTokens
- Nueva entidad `IdenExtTokens` con campos: RefreshToken (cifrado), ScopesHash, ExpiresAt, RefreshExpiresAt, LastRefresh, Revoked
- Relación N:1 con `IdenExt`
- EF Configuration con índices: `IX_IdenExtTokens_IdIdenExt`, `IX_IdenExtTokens_Activos` (filtrado)
- Repository (`IIdenExtTokensRepository`) con `ObtenerTokenActivoAsync()` y `RevocarTokensAsync()`
- Registro en `DatosDependencyInjection.cs`
- SQL migration en `Migrations/FASE17_OAuth2_Certification.sql`

### FASE 17.8 — IOAuthSessionStore
- Interfaz `IOAuthSessionStore` extraída de `OAuthSessionStore`
- Implementación Memory (`MemoryOAuthSessionStore`) para desarrollo
- Preparada para implementaciones Redis/SQL Server en producción
- Registro via DI como `IOAuthSessionStore`

### FASE 17.9 — IUsedAuthorizationCodeStore
- Interfaz `IUsedAuthorizationCodeStore` extraída de `UsedCodeStore`
- Implementación Memory (`MemoryUsedCodeStore`) para desarrollo
- Preparada para implementaciones Redis/SQL Server en producción
- Registro via DI como `IUsedAuthorizationCodeStore`

### FASE 17.10 — Provider Factory por DI
- Provider resolution utiliza `IEnumerable<IExternalIdentityProvider>` inyectado via DI con `FirstOrDefault(p => p.ProviderCode == providerCode)`
- Sin `switch`/`if`/`else` para selección de proveedor. Cumple regla #8

---

### FASE 17.11 — Expansión ConfProvIden (endpoints OAuth2)
- Nuevos campos en entidad: `AuthorizationEndpoint`, `TokenEndpoint`, `JwksUri`, `Issuer`, `ResponseType`, `GrantType`, `ExtraParams`
- EF Configuration actualizada con propiedades, max lengths y defaults
- DTOs actualizados: `ConfProvIdenDto`, `CrearConfProvIdenDto`, `ActualizarConfProvIdenDto`
- `ConfProvIdenService.CrearAsync()` pasa los nuevos parámetros al factory `ConfProvIden.Crear()`

### FASE 17.12 — Dashboard KPIs OAuth
- Nuevos campos en `DashboardOAuthDto`: `TokenRefreshUsage`, `RevocacionesTokens`, `RevocacionesConsent`, `TasaExito`
- `DashboardEnterpriseService.GetOAuthAsync()` consulta `IIdenExtTokensRepository` para conteo de refrescos y revocaciones
- Tasa de éxito calculada desde `AudIdenExt` (logins exitosos / total)

### FASE 17.13 — Auditoría ampliada
- `AudIdenExt` ya contenía todos los campos necesarios (HttpStatus, TiempoRespuesta, Scopes, Origen, Destino, Browser, OS, etc.)
- Registro completo en `ExternalAuthService.LoginExternoAsync()` desde ETAPA 12

### FASE 17.14 — Login UI redesign
- Loading por proveedor (`_loadingProvider` + `MudProgressCircular` en botón individual)
- Collapse responsive: solo primeros 3 proveedores visibles, resto en "Más opciones" (`MoreHoriz`)
- Tooltips desde `ConfProvIden.Tooltip` ya implementados
- Errores diferenciados por proveedor ya implementados

### FASE 17.15 — Informe final de certificación
- Documentado en `Docs/FASE17_OAuth2_Certificacion.md`
- Migración SQL en `Migrations/FASE17_OAuth2_Certification.sql`
- Build 0 errores C# en todos los proyectos

---

## Google Cloud Console — Configuración Exacta

| Campo | Valor |
|-------|-------|
| Authorized JavaScript Origins | `https://localhost:7275` |
| Authorized Redirect URIs | `https://localhost:5001/api/auth/externo/GOOGLE/callback` |
| Application type | Web application |
| OAuth consent screen | External (testing) |
| Scopes | openid, email, profile |

---

## Build Status
- 0 errores C#, 0 warnings nuevas (solo preexistentes MUD0002 en Blazor)
- Solución compila correctamente (`dotnet build PassPlat.slnx`)

---

## Archivos Afectados

| Archivo | Cambio |
|---------|--------|
| `PassPlat.Dominio/Entities/Core/IdenExtTokens.cs` | Nueva entidad |
| `PassPlat.Datos/Configurations/Core/IdenExtTokensConfiguration.cs` | Nueva EF config |
| `PassPlat.Datos/Repositories/IdenExtTokensRepository.cs` | Nuevo repository |
| `PassPlat.Datos/DatosDependencyInjection.cs` | Registro IdenExtTokensRepository |
| `PassPlat.Aplicacion/Services/OAuthSessionStore.cs` | Interfaz + Memory implementación |
| `PassPlat.Aplicacion/Services/UsedCodeStore.cs` | Interfaz + Memory implementación |
| `PassPlat.Aplicacion/Services/ExternalAuthService.cs` | Inyecta interfaces store |
| `PassPlat.Aplicacion/AplicacionDependencyInjection.cs` | Registro interfaces store |
| `PassPlat.WebAPI/Controllers/ExternalAuthController.cs` | IOAuthSessionStore, RedirectUri fijo |
| `PassPlat.WebAPI/Program.cs` | CORS, HSTS, HttpsRedirection |
| `PassPlat.WebAPI/Properties/launchSettings.json` | HTTPS profile |
| `PassPlat.Aplicacion/Services/GoogleIdentityProvider.cs` | access_type=offline |
| `PassPlat.Web/wwwroot/appsettings.json` | ApiBaseUrl HTTPS |
| `PassPlat.Web/Pages/Federacion/SigninGoogle.razor` | ELIMINADO |
| `AGENTS.md` | Reglas OAuth2 |
| `Migrations/FASE17_OAuth2_Certification.sql` | Script SQL |
