# OAuth Data Model — PassPlat

> Generado: 2026-07-22  
> Propósito: Definir el modelo de datos OAuth actual y la hoja de ruta hacia un modelo expansible para cualquier proveedor externo de identidad.  
> Estado: Implementación actual (FASE 17 completa)

---

## Modelo Actual (FASE 17)

```
ProvIden (catálogo global, 7 registros)
│   Código: GOOGLE, MICROSOFT, GITHUB, APPLE, LINKEDIN, FACEBOOK, INSTAGRAM
│   MetadataJson: { "authorization_endpoint": "...", "token_endpoint": "...", ... }
│
└── ConfProvIden (por tenant, 1 por proveedor)
        ClientId (cifrado)
        ClientSecret (cifrado)
        Callback
        PermitirAutoLink
        AutoProvisionar
        OrdenVisual, Logo, Color, Tooltip
        RowVersion (concurrencia)
```

**Limitaciones del modelo actual:**
- `MetadataJson` es un campo JSON que concentra: endpoints, scopes, claim mappings, well-known URLs
- Sin tabla dedicada para scopes por proveedor
- Sin tabla dedicada para claim mappings
- Sin tabla de endpoints (mezclados en MetadataJson)
- Sin tabla de well-known metadata cache
- El refresh token se almacena en `IdenExtTokens` (correcto, implementado en FASE 17.7)

---

## Modelo Futuro (hoja de ruta)

```
ProvIden (catálogo global)
│   Código, Nombre, Activo
│   MetadataJson (solo para retrocompatibilidad)
│
├── ConfProvIden (por tenant)
│       IdProvIden, IdTenant
│       ClientId (cifrado AES-256-GCM)
│       ClientSecret (cifrado AES-256-GCM)
│       Callback (desde BD, regla #23)
│       PermitirAutoLink, AutoProvisionar
│       OrdenVisual, Logo, Color, Tooltip
│       RowVersion
│
├── OAuthScopes (NUEVA — por ConfProvIden)
│       IdConfProvIden
│       Scope (string: "openid", "email", "profile", ...)
│       EsRequerido (bit)
│       Descripcion
│       Orden (smallint)
│
├── OAuthClaims (NUEVA — por Scope)
│       IdOAuthScope
│       Claim (string: "sub", "email", "name", ...)
│       EsRequerido (bit)
│       Descripcion
│
├── OAuthClaimMappings (NUEVA — por ConfProvIden)
│       IdConfProvIden
│       ClaimOrigen (string: claim del proveedor)
│       ClaimDestino (string: claim interno PassPlat)
│       EsIdentificador (bit: si es el claim que identifica al usuario)
│       Transformacion (string opcional: lower, upper, regex)
│
├── OAuthEndpoints (NUEVA — por ConfProvIden)
│       IdConfProvIden
│       Tipo (nvarchar: Authorization, Token, UserInfo, Revocation, JWKS)
│       Url (nvarchar(2048))
│       Metodo (nvarchar: GET, POST)
│       EsPredeterminado (bit)
│
├── OAuthWellKnown (NUEVA — cache por ConfProvIden)
│       IdConfProvIden
│       UltimaActualizacion (datetime)
│       MetadataJson (nvarchar(max))
│       FirmaJWKS (nvarchar, hash de las keys)
│       Expiracion (datetime)
│
├── IdenExt (identidades vinculadas)
│       IdUsuario, IdProvIden, IdEstado
│       IdExterno (sub del proveedor)
│       EmailExterno, NombreExterno
│       UltimoLogin
│
├── IdenExtTokens (tokens de refresco cifrados)
│       IdIdenExt
│       RefreshToken (cifrado AES-256-GCM)
│       AccessToken (cifrado, opcional)
│       FechaEmision, FechaExpiracion
│       EsRevocado
│
├── HistorialIdenExt (auditoría de eventos OAuth)
│       IdIdenExt, IdTipoAuditoria
│       Evento, Detalle, IP, UserAgent
│
└── AudIdenExt (auditoría extendida)
        IdUsuario, IdProvIden, IdResultado
        CorrelationId, IP, UserAgent
        LatenciaMs
```

---

## Hoja de Ruta para Expansión

| FASE | Tabla | Estado | Prioridad |
|------|-------|--------|-----------|
| 17.7 | IdenExtTokens | ✅ Implementado | — |
| Pendiente | OAuthScopes | 📝 Pendiente | Alta (sin scopes no se puede validar granularidad) |
| Pendiente | OAuthClaims | 📝 Pendiente | Media (derivado de scopes) |
| Pendiente | OAuthClaimMappings | 📝 Pendiente | Alta (necesario para mapear claims de cualquier proveedor) |
| Pendiente | OAuthEndpoints | 📝 Pendiente | Alta (necesario para proveedores sin well-known) |
| Pendiente | OAuthWellKnown | 📝 Pendiente | Media (cache de metadata OIDC) |

### FASE 17.11 — ConfProvIden expansion (pendiente)
- Migrar `MetadataJson` a tablas normalizadas
- Agregar `OAuthScopes`, `OAuthEndpoints`
- Actualizar `ConfProvIdenService` para CRUD de scopes y endpoints
- UI: MudTable para scopes y endpoints en el dialog de ConfProvIden

### FASE 17.12 — Claim mappings (pendiente)
- Crear `OAuthClaimMappings`
- Implementar en `ExternalAuthService` la transformación de claims
- Soportar cualquier proveedor sin cambios de código

### FASE 17.13 — Well-known cache (pendiente)
- Crear `OAuthWellKnown`
- Implementar background service para refresco periódico
- Cachear JWKS, endpoints, metadata OIDC

---

## Principios de diseño

1. **ProvIden es catálogo global**: No depende de tenant. Solo se agregan nuevos proveedores vía migración SQL.
2. **ConfProvIden es por tenant**: Cada tenant configura sus propios ClientId/Secret, scopes, endpoints.
3. **Secretos cifrados**: ClientSecret, RefreshToken, AccessToken siempre cifrados con AES-256-GCM vía `IEncryptionService`.
4. **Sin switch/if por proveedor**: La resolución usa `IExternalIdentityProviderFactory` con DI (`IEnumerable<IExternalIdentityProvider>`). Nuevos proveedores = nueva implementación registrada en DI.
5. **Endpoints desde BD**: RedirectUri, AuthorizationEndpoint, TokenEndpoint desde `ConfProvIden`/`OAuthEndpoints`, nunca desde `appsettings.json` o `HttpContext`.
6. **Compatibilidad hacia atrás**: `MetadataJson` se mantiene en `ProvIden` hasta que todas las tablas nuevas estén implementadas. Luego se depreca.
7. **Scopes específicos por tenant**: No todos los tenants necesitan los mismos scopes. Cada `ConfProvIden` tiene sus `OAuthScopes`.
8. **Claim mappings configurables**: Cada tenant puede mapear claims del proveedor a claims internos de PassPlat, permitiendo integrar cualquier proveedor OIDC/OAuth2 sin cambiar código.
