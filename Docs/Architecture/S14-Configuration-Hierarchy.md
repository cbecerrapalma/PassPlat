# S14 — Configuration Hierarchy

> Sprint S14 · FASE F2 (read-only) · Define el orden de resolución PLATFORM → APP → TENANT.
> Complementa `S14-Configuration-Scope-Matrix.md` (F1).

---

## Orden de resolución estándar (PLATFORM → APP → TENANT)

Para toda configuración que admite override por app o por tenant se resuelve en este orden
(**first-match gana**), reutilizando el patrón del resolver de email (`EmailAccountResolverService`):

```
1) APP+TENANT  (más específica)   → si existe y activa, se usa
2) APP          (app global)      → si existe y activa, se usa
3) TENANT       (tenant global)   → si existe y activa, se usa
4) PLATFORM     (default global)  → si existe y activa, se usa
5) primer activo / error
```

Cuando la tabla no tiene columna `IdApp` (según F1), se omite el paso APP y se resuelve
directamente TENANT → PLATFORM.

---

## Resolución por dominio (estado actual)

### 1. Email (ya correcto — patrón de referencia)

`EmailAccountResolverService.ResolveAsync(int? idApp, int? idTenant)` implementa:
**APP → TENANT → GLOBAL**. ✅ Cumple la jerarquía.

Tablas implicadas: `AppEmailAccounts` (APP), `TenantEmailAccounts` (TENANT), `EmailAccounts` (PLATFORM).

### 2. OAuth / ConfProvIden (**GAP F3**)
El `ConfProvIden` es **TENANT-only** (F1: `IdApp=FALSE`, `IdTenant=TRUE`).
`ExternalAuthService` lo resuelve correctamente por tenant:
- `ObtenerConfiguracionAsync(idTenant, idProvIden)` (L122, L343).

**Problema**: `GenerateAuthorizationUrlAsync(string providerCode, int idTenant, int idApp = 1, ...)`
tiene un **default hardcodeado `idApp = 1`** en la firma (L33). Cuando el caller no pasa `idApp`,
la URL de autorización se genera con `IdApp=1` (PASSPLAT) en lugar del IdApp real del contexto
(`IdApp` claim del JWT / app seleccionada en UI). *Este es el único cambio funcional del Sprint*.

### 3. ConfigApp — TENANT generales
`ConfigApp` es TENANT-only. No hay override por app; los valores generales del tenant se leen
directamente. Sin gap.

### 4. Políticas de contraseña — APP_TENANT
`PoliticasPwd` es APP_TENANT (F1). La resolución actual de la política más estricta ya aplica la
union multi-tenant y debe **añadir el nivel APP** antes del TENANT según la matriz. Pendiente
verificación en F5–F11 (dominio read-only).

---

## Resumen de gaps detectados (para F3)

| GAP | Ubicación | Severidad | Cambio |
|-----|-----------|-----------|--------|
| `idApp = 1` default | `ExternalAuthService.cs:33` | ALTA (OAuth multi-app) | Propagar `idApp` real del contexto |
| URL OAuth IdApp param | `ExternalAuthService.cs:375` | ALTA | Usar `idApp` (no 1) |

Los demás passos (F5–F11) son read-only y no requieren cambio funcional si la matriz se respeta.

---

*Siguiente paso*: **F3 — Fix OAuth App Context** (`ExternalAuthService.cs:289`, `:33`, `:329`, `:375`).