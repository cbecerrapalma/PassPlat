# U01 — MFA.IdTenant: Decisión Formal

## Estado: KEEP (EXECUTION CONTEXT)

## 1. Evidencia

### 1.1 SP_Auth_Login (línea 1657) — NO filtra por IdTenant
```sql
SELECT @IdMFAPrincipal = Id FROM dbo.MFA
WHERE IdUsuario = @IdUsuario AND EsPrincipal = 1
AND IdEstado = (SELECT Id FROM dbo.EstadosMFA WHERE Codigo = 'ACTIVO');
```
El SP de autenticación principal **no usa IdTenant** para determinar si el usuario requiere MFA. El MFA es global al usuario.

### 1.2 UX_MFA_Principal — UNIQUE global, no por tenant
```sql
CREATE UNIQUE INDEX UX_MFA_Principal
  ON dbo.MFA (IdUsuario)
  WHERE ([EsPrincipal]=(1));
```
El índice único filtrado es sobre `IdUsuario` solamente. Un usuario puede tener MFA registrado en múltiples tenants, pero solo **un método principal globalmente**.

### 1.3 Repository — lecturas sin IdTenant
- `ObtenerMetodoPrincipalAsync(int idUsuario)` — solo filtra por `IdUsuario`
- `ObtenerMetodosPorUsuarioAsync(int idUsuario)` — solo filtra por `IdUsuario`
- `RevocarMetodo(int idUsuario, int idMFARegistro)` — solo filtra por `IdUsuario`

### 1.4 SP_MFA_Validar (línea 747) — Sí filtra por IdTenant
```sql
WHERE IdUsuario = @IdUsuario AND IdTenant = @IdTenant
AND IdTipoMFA = @IdTipoMFA AND IdMFA = @IdMFA
AND IdEstado = @IdEstadoActivo;
```
La validación del código MFA **sí usa IdTenant**. Esto significa que el código de un usuario en Tenant A no puede usarse en Tenant B.

### 1.5 Controller — IdTenant = execution context
- `Registrar`: `dto.IdTenant = _tenantContext.CurrentId` — se asigna desde el contexto de ejecución
- `Validar`: usa `_tenantContext.CurrentId` para validación, NO el `IdTenant` del request

### 1.6 AuthService.CompletarLoginConMFAAsync — flujo mixto
1. Obtiene método principal SIN filtrar tenant (`_mfaRepo.ObtenerMetodoPrincipalAsync`)
2. Valida código CON tenant (`_mfaRepo.ValidarMFAAsync` con `idTenant`)
3. Crea `AuthenticationContext` con `idTenant`

## 2. Justificación semántica

**MFA.IdTenant representa EXECUTION CONTEXT, NO MEMBERSHIP.**

- Un usuario autenticado en Tenant A registra un método MFA. El `IdTenant` en MFA registra *dónde* se registró, no *para qué* tenant funciona.
- El login busca el método principal independientemente del tenant.
- La validación del código MFA usa el tenant actual para evitar cross-tenant reuse del mismo código.
- El usuario tiene **un solo método MFA principal** globalmente, pero puede tener MFA secundario en diferentes tenants (por ejemplo, TOTP en Tenant A, SMS en Tenant B).

## 3. Impacto en A1 (Modelo UsuarioTenant)

### 3.1 ¿Cambia algo con el nuevo modelo?
**No.** MFA.IdTenant sigue siendo EXECUTION CONTEXT en el nuevo modelo:

| Escenario | Comportamiento |
|-----------|----------------|
| Usuario miembro de Tenant A y B | Login en A usa el método principal global. Código validado contra A. Login en B usa el mismo método principal global. Código validado contra B. |
| Context-switch A→B | MFA ya validado en login. No se revalida. Configuración MFA sigue siendo la misma. |
| Platform Context (IdTenant=NULL) | MFA lookup (sin IdTenant) funciona igual. Validación en Platform Context debe permitir IdTenant=NULL en SP_MFA_Validar. |
| Platform JWT vs Tenant JWT | Ambos usan el mismo MFA lookup global. Diferencia solo en claims de tenant. |

### 3.2 Cambios necesarios en A1
| Cambio | Razón | Prioridad |
|--------|-------|-----------|
| SP_Auth_Login: MFA lookup sigue sin IdTenant | No cambiar — es correcto | N/A |
| `ObtenerMetodoPrincipalAsync`: sigue sin IdTenant | No cambiar — es correcto | N/A |
| SP_MFA_Validar: soportar @IdTenant=NULL | Necesario para Platform Context | P1 (cuando se implemente Platform JWT) |
| Registro MFA en Platform Context: permitir IdTenant=NULL | Nuevo: Platform users pueden tener MFA | P1 |
| `ValidarMfaRequest.IdTenant` no depende de usuario.IdTenant | Validar contra _tenantContext.CurrentId, ya implementado | ✅ ya OK |

### 3.3 Lo que NO cambia
- Tabla MFA: estructura existente se mantiene (KEEP)
- FK a Tenant: se mantiene (IdTenant sigue siendo FK a Tenant, NULL permitido)
- UX_MFA_Principal: se mantiene como UNIQUE (IdUsuario) filtrado
- Repository métodos: sin cambios en firma
- Service métodos: sin cambios en firma

## 4. Matriz de impacto completa

| Dimensión | Impacto | Detalle |
|-----------|---------|---------|
| **Tablas afectadas** | ✅ Ninguna | MFA table permanece igual |
| **SPs afectados** | ⚠️ SP_MFA_Validar | Soportar @IdTenant=NULL para Platform Context (P1) |
| **Servicios C#** | ✅ Ninguno | Sin cambios en AuthService, MfaService |
| **Controllers** | ✅ Ninguno | MfaController ya usa _tenantContext.CurrentId |
| **Authentication** | ✅ Ninguno | MFA lookup global ya no usa IdTenant |
| **Context-switch** | ✅ Ninguno | MFA ya validado. No revalida en switch |
| **MFA/OAuth** | ✅ Ninguno | OAuth callback usa mismo flujo de MFA |
| **Índices** | ✅ Ninguno | UX_MFA_Principal se mantiene |
| **FKs** | ⚠️ FK a Tenant | Permitir NULL si Platform Context requiere MFA |
| **Migración BD** | ✅ Ninguna | Sin cambios DDL |
| **Rollback** | ✅ Ninguno | Sin cambios → sin rollback |

## 5. Conclusión

**Decisión: KEEP**

MFA.IdTenant permanece como EXECUTION CONTEXT. No constituye pertenencia del usuario al tenant. Las operaciones críticas de autenticación (SP_Auth_Login, ObtenerMetodoPrincipalAsync) ya ignoran IdTenant correctamente para el lookup, y usan IdTenant solo para la validación del código (SP_MFA_Validar).

El único trabajo pendiente es soportar `@IdTenant=NULL` en SP_MFA_Validar cuando se implemente Platform Context (A1.5/A1.6). Esto no bloquea A1.1 SQL Schema.

**U01 — RESUELTO. No bloquea A1.1.**
