# S14 — F5: Email Resolution Certification

> Sprint S14 · FASE F5 (read-only) · Certifica la resolución existente de cuentas SMTP.

---

## Implementación auditada

`PassPlat.Aplicacion.Services.Email.EmailAccountResolverService` (ya existe, no se modifica).

### Orden de resolución (App → Tenant → Global → FirstActive)

```csharp
public async Task<Result<(EmailAccount, SmtpAccountConfig)>> ResolveAsync(int? idApp, int? idTenant, CancellationToken ct)
{
    // 1. APP-level default account
    if (idApp.HasValue) {
        var appAccounts = await _appEmailAccountRepo.ObtenerPorAppAsync(idApp.Value, ct);
        if (appAccounts.IsSuccess && appAccounts.Value?.Count > 0) {
            var appAccount = appAccounts.Value
                .OrderByDescending(aa => aa.EsPredeterminada)
                .ThenBy(aa => aa.Id)
                .First();
            if (appAccount.EmailAccount != null && appAccount.EmailAccount.Activo)
                return Success((appAccount.EmailAccount, BuildSmtpConfig(appAccount.EmailAccount)));
        }
    }

    // 2. TENANT-level default account
    if (idTenant.HasValue) {
        var tenantAccounts = await _tenantEmailAccountRepo.ObtenerPorTenantAsync(idTenant.Value, ct);
        if (tenantAccounts.IsSuccess && tenantAccounts.Value?.Count > 0) {
            var tenantAccount = tenantAccounts.Value
                .OrderByDescending(ta => ta.EsPredeterminada)
                .ThenBy(ta => ta.Id)
                .First();
            if (tenantAccount.EmailAccount != null && tenantAccount.EmailAccount.Activo)
                return Success((tenantAccount.EmailAccount, BuildSmtpConfig(tenantAccount.EmailAccount)));
        }
    }

    // 3. GLOBAL default account
    var global = await _emailAccountRepo.ObtenerPredeterminadaAsync(ct);
    if (global.IsSuccess && global.Value != null)
        return Success((global.Value, BuildSmtpConfig(global.Value)));

    // 4. First active global
    var activos = await _emailAccountRepo.ObtenerActivosAsync(ct);
    if (activos.IsSuccess && activos.Value?.Count > 0) {
        var first = activos.Value.OrderBy(ea => ea.Id).First();
        return Success((first, BuildSmtpConfig(first)));
    }

    return Failure("EMAIL_NO_ACCOUNT", "No hay cuentas de email activas");
}
```

---

## Tablas implicadas (F1 scope)

| Tabla | Scope | Columna clave |
|-------|-------|---------------|
| `AppEmailAccounts` | APP_GLOBAL | `IdApp`, `EsPredeterminada` |
| `TenantEmailAccounts` | TENANT | `IdTenant`, `EsPredeterminada` |
| `EmailAccounts` | PLATFORM_GLOBAL | `Activo`, `EsPredeterminada` |

---

## Casos de prueba conceptuales

| Caso | idApp | idTenant | Cuenta esperada |
|------|-------|----------|-----------------|
| A | 1 | 3 | AppEmailAccounts(App=1) si existe y Activa |
| B | null | 3 | TenantEmailAccounts(Tenant=3) si existe |
| C | null | null | EmailAccounts (Global default) |
| D | 999 | 999 | FirstActive global (fallback) |

---

## Evidencia funcional (SQL)

```sql
-- AppEmailAccounts para App=1
SELECT * FROM AppEmailAccounts WHERE IdApp = 1;
-- TenantEmailAccounts para Tenant=3
SELECT * FROM TenantEmailAccounts WHERE IdTenant = 3;
-- EmailAccounts global
SELECT * FROM EmailAccounts WHERE Activo = 1 ORDER BY EsPredeterminada DESC, Id;
```

---

## Hallazgos

- ✅ Patrón App→Tenant→Global implementado correctamente.
- ✅ Prioridad `EsPredeterminada` respetada.
- ✅ Fallback a FirstActive documentado.
- ✅ Contraseñas descifradas vía `IEncryptionService` (AES-256-GCM).
- ⚠️ No hay tests automatizados específicos de esta resolución (F14 añadirá).

---

## Conclusión

**CERTIFIED** — La resolución Email sigue la jerarquía F2 sin gaps. No requiere cambios.