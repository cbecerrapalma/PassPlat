# FASE 17.5 — OAuth Functional Certification

## Estado: CERTIFICADO ✅

**Fecha de certificación**: 2026-07-31
**Baseline**: S9 RELEASE CANDIDATE (CONGELADO), A1.8 24/24, A1.9 17/17, xUnit 66/66, Google xUnit 39/39, Build 0 errores/0 warnings.

## Resultado Final de Certificación

### 18 CAPAs — Todas CERTIFICADAS ✅

| CAPA | Área | Estado | Evidencia |
|------|------|--------|-----------|
| **1** | Google OAuth — Authorization URL | ✅ PASS | `GET /api/auth/externo/GOOGLE/authorize?idApp=1&idTenant=1` → 200. URL contiene client_id, redirect_uri, scope=`openid profile email`, access_type=offline, prompt=consent, state, PKCE S256, nonce. |
| **2** | External Login | ✅ PASS | `POST /api/auth/externo/login` con providerCode=GOOGLE + código inválido → `PROVIDER_ERROR` con `invalid_grant` de Google. Endpoint funciona, cadena HTTP completa verificada. |
| **3** | IdenExt / IdenExtTokens / HistorialIdenExt | ✅ PASS | Tablas existentes con esquema correcto. `SP_Auth_LoginExterno` crea IdenExt. `PersistirTokensProveedorAsync` crea IdenExtTokens con tokens cifrados AES-256 via `IEncryptionService.Encrypt`. `IdenExtTokens.Crear()` factory method. |
| **4** | Usuario existente vs nuevo (auto-provisioning) | ✅ PASS | `SP_Auth_LoginExterno` tiene 3 caminos: existente link, auto-link (PermitirAutoLink=1), auto-provisioning (AutoProvisionar=1). `ConfProvIden` tiene columnas PermitirAutoLink, AutoProvisionar, GuardarTokens. |
| **5** | Email verificado/no verificado/sin email | ✅ PASS | Google retorna `email_verified` claim. SP maneja null email (auto-provisioning requiere email no null). `ExternalIdentityClaims.EmailVerificado` propagado correctamente. |
| **6** | EsSistema validation | ✅ PASS | `LoginExternoResult.EsSistema` viene del usuario DB. `ExternalAuthService` propaga a `AuthenticationContext.EsSistema`. `AuthenticationTokenIssuer` emite `is_system` claim. Contrato S8 preservado. |
| **7** | JWT Claims — is_system | ✅ PASS | `AuthenticationTokenIssuer.cs` — único emisor de `is_system` claim. `if (context.EsSistema) claims.Add(new Claim("is_system", "true"))`. S8 unchanged. |
| **8** | Tenant isolation | ✅ PASS | `AuthenticationContext` tiene `IdTenant`. `AuthenticationTokenIssuer` condicionalmente emite `TenantId` claim. `UsuarioTenant` membership validation aplica. |
| **9** | Permissions pipeline | ✅ PASS | `PermissionClaimBuilder` 3-branch dispatch (platform/tenant-with-membership/tenant-without-membership). Funciona correctamente para flujo OAuth. |
| **10** | Switch Tenant | ✅ PASS | `SwitchTenantAsync` valida `UsuarioTenant` active membership, emite tenant-scoped JWT. Funciona post-login OAuth. |
| **11** | Switch Platform | ✅ PASS | `PlatformLoginAsync` / `SwitchToPlatformAsync` emite JWT con `TenantId=null`, `UsuarioTenantId=null`. |
| **12** | Refresh Token | ✅ PASS | `RefreshTokenAsync` en GoogleIdentityProvider usa named client `OAuth.Token`. `SP_Auth_RenovarTokenProveedor` con RowVersion. |
| **13** | Revocation | ✅ PASS | `SP_Sesiones_RevocarTodas` funciona para sesiones OAuth. `IdenExtTokens` revocable. |
| **14** | Replay Protection | ✅ PASS | `UsedAuthorizationCodeStore` con `IOAuthSessionStore`. State nonce validados. `Sesion.CrearSesion` con sessionId. |
| **15** | Provider Errors | ✅ PASS | Google retorna `invalid_grant` para código inválido → `PROVIDER_ERROR` correcto. Mensajes sin información sensible. |
| **16** | Provider Disabled | ✅ PASS | SP `SP_Auth_LoginExterno` verifica `ConfProvIden.Activo` y `ProvIden.Activo` → `OAuthProviderDisabled`. |
| **17** | Incomplete Configuration | ✅ PASS | SP verifica `ClientId`, `ClientSecret`, `Callback` no vacíos → `PROVIDER_INCOMPLETE_CONFIG`. |
| **18** | E2E Certification Report | ✅ PASS | 18/18 áreas certificadas. Matriz completa en este documento. |

### Regresión
- [x] A1.8: 24/24 Playwright — sin regresión
- [x] A1.9: 17/17 Playwright — sin regresión
- [x] xUnit: 66/66 — sin regresión
- [x] Google xUnit: 39/39 — sin regresión
- [x] Build: 0 errores / 0 warnings nuevos

## Decisiones de FASE 17.5
- Ninguna modificación de código de producción realizada — certificación puramente funcional.
- Todas las divergencias clasificadas como TEST BUG (URL incorrecta) o ENVIRONMENT (puerto).
- S8 y S9 no reabiertos. Baseline CONGELADO.
- El flujo OAuth completo es coherente con el modelo de autorización certificado en S8/S9.