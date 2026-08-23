# S14 — F8: Resolvers Audit

> Sprint S14 · FASE F8 (read-only) · Inventario de resolvers de configuración: reutilizar > extender > centralizar > crear.

---

## Criterio de decisión

| Acción | Cuándo aplicar |
|--------|----------------|
| **REUTILIZAR** | Resolver existente cubre el caso; misma prioridad y tablas. |
| **EXTENDER** | Resolver base sirve, pero necesita parámetro extra o lógica menor. |
| **CENTRALIZAR** | Múltiples resolvers hacen lo mismo → unificar en uno genérico. |
| **CREAR** | Ningún resolver existente cubre el caso; nuevo dominio. |

---

## Inventario de resolvers existentes

| # | Resolver | Ubicación | Entradas | Salida | Prioridad | Tablas | Estado |
|---|----------|-----------|----------|--------|-----------|--------|--------|
| 1 | `EmailAccountResolverService` | `PassPlat.Aplicacion.Services.Email` | `int? idApp, int? idTenant` | `(EmailAccount, SmtpAccountConfig)` | App → Tenant → Global → FirstActive | `AppEmailAccounts`, `TenantEmailAccounts`, `EmailAccounts` | ✅ Certificado F5 |
| 2 | `ExternalLoginProviderService.ObtenerDisponiblesAsync` | `PassPlat.Aplicacion.Services.OAuth` | `int idTenant` | `IReadOnlyList<ExternalLoginProviderDto>` | Tenant → Platform (herencia `MostrarProveedoresPlataforma`) | `ConfProvIden`, `ProvIden`, `ConfigApp` | ✅ Funcional |
| 3 | `ConfigAppRepository.ObtenerPorClaveAsync` | `PassPlat.Datos.Repositories` | `string clave, int? idTenant` | `ConfigApp?` | Tenant directo (sin fallback automático) | `ConfigApp` | ✅ Básico |
| 4 | `TenantInitializer.InitializeAsync` | `PassPlat.WebAPI.MultiTenant` | `string tenantCode` | `Result<int>` (IdTenant) | Header `X-Tenant-Code` → DB | `Tenants`, `DominiosTenant` | ✅ Multi-tenant |
| 5 | `UsuarioTenantRepository.ResolverIdUsuarioTenantAsync` | `PassPlat.Datos.Repositories` | `int idUsuario, int idTenant` | `int?` (IdUsuarioTenant) | Membership directa | `UsuarioTenant` | ✅ A1 multi-tenant |
| 6 | `PasswordPolicyResolver` (implícito en `PoliticaPwdService`) | `PassPlat.Aplicacion.Services` | `int idTenant, int? idApp, int idUsuario` | `PoliticaPwd` | App+Tenant → Tenant → AppGlobal → Platform → Default | `PoliticasPwd`, `RolesPoliticasPwd`, `Roles`, `UsuariosPermisos`, `Accesos` | ⚠️ Complejo |

---

## Análisis por dominio

### Email (Resolver #1)
- **Estado**: Certificado F5. Patrón canónico App→Tenant→Global→FirstActive.
- **Acción**: **REUTILIZAR** como plantilla para nuevos resolvers.

### OAuth Providers (Resolver #2)
- **Estado**: Funcional. Usa herencia plataforma→tenant vía `ConfigApp`.
- **Gap**: No recibe `idApp` (ConfProvIden no tiene IdApp). Correcto por F1.
- **Acción**: **REUTILIZAR**.

### ConfigApp (Resolver #3)
- **Estado**: Básico. Solo lookup directo por tenant + clave.
- **Gap**: No implementa fallback plataforma automático (excepto lógica manual en #2).
- **Acción**: **EXTENDER** — agregar método `ObtenerConHerenciaAsync(clave, idTenant)` que implemente Tenant→Platform genérico.

### Tenant Resolution (Resolver #4)
- **Estado**: Certificado A1. Header `X-Tenant-Code` + dominio → IdTenant.
- **Acción**: **REUTILIZAR**.

### UsuarioTenant (Resolver #5)
- **Estado**: Certificado A1. Membership directa.
- **Acción**: **REUTILIZAR**.

### Password Policy (Resolver #6)
- **Estado**: Lógica distribuida en `PoliticaPwdService.ObtenerPoliticaAplicableAsync`.
- **Complejidad**: Une `PoliticasPwd` (APP_TENANT) + `RolesPoliticasPwd` + `Roles` + `UsuariosPermisos` + `Accesos`.
- **Gap**: No centralizado; lógica repetida en `UsuarioService`, `AuthService`, `PasswordService`, `PasswordExpirationBackgroundService`.
- **Acción**: **CENTRALIZAR** → crear `IPasswordPolicyResolver` único con método `ObtenerPoliticaAplicableAsync(idUsuario, idTenant, idApp)`.

---

## Resolvers faltantes / necesarios

| Dominio | Necesidad | Recomendación |
|---------|-----------|---------------|
| **ConfigApp genérico** | Fallback Tenant→Platform automático | **EXTENDER** #3: `ObtenerConHerenciaAsync` |
| **Password Policy** | Centralizar lógica dispersa | **CENTRALIZAR** #6: nuevo `PasswordPolicyResolver` |
| **Email Templates** | Resolución Tenant→Platform + idioma | **CREAR** nuevo `EmailTemplateResolver` (F6: NOT REQUIRED IdApp) |
| **Dashboard/Config** | ConfigApp con herencia | Usar **EXTENDER** #3 |

---

## Matriz de decisión final

| Resolver | Acción | Prioridad | Esfuerzo |
|----------|--------|-----------|----------|
| EmailAccountResolver | REUTILIZAR | — | 0 |
| ExternalLoginProviderService | REUTILIZAR | — | 0 |
| ConfigAppRepository | EXTENDER (herencia genérica) | Media | Bajo |
| TenantInitializer | REUTILIZAR | — | 0 |
| UsuarioTenantRepository | REUTILIZAR | — | 0 |
| PasswordPolicy (disperso) | CENTRALIZAR → nuevo `PasswordPolicyResolver` | Alta | Medio |
| EmailTemplateResolver | CREAR (F6) | Baja | Bajo |

---

## Recomendación inmediata (S14)

1. **No crear resolvers nuevos en S14** (fase read-only).
2. Documentar deuda técnica: `PasswordPolicyResolver` centralizado → backlog próxima sprint.
3. `ConfigAppRepository.ObtenerConHerenciaAsync` → improvement bajo riesgo.
4. `EmailTemplateResolver` → solo si F15/F16 demuestran necesidad real.

---

## Conclusión

**Patrón canónico establecido**: `EmailAccountResolverService` (App→Tenant→Global→FirstActive).  
**Deuda técnica identificada**: `PasswordPolicy` disperso → centralizar en próximo sprint.  
**No se crean resolvers en S14** — solo auditoría.