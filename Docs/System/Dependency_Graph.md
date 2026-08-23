# Dependency Graph — PassPlat

> Generado: 2026-07-22  
> Propósito: Determinar orden seguro para DELETE, MERGE, INSERT y RESET sin violar FK.  
> Total tablas: 55

## Convención

```
TABLA_A ──→ TABLA_B
```
`TABLA_A` depende de `TABLA_B` (TABLA_A tiene FK → TABLA_B).

---

## Nivel 0 — Sin dependencias (Catálogo raíz)

Estas tablas no tienen FK que apunten a otras tablas del sistema. Se crean primero, se eliminan último.

```
EstadosUsr
EstadosMFA
EstIdenExt
TiposMFA
TiposDisp
TiposCambioPwd
TiposBloqueo
TiposAuditoria
TipAsigPermiso
TiposModulo
ResultadosAcceso
ProvIden
EmailProviders
Apps
```

## Nivel 1 — Dependen solo de Nivel 0

```
Modulos          ──→ TiposModulo
Permisos         ──→ Modulos
Roles            ──→ (sin FK a catálogo)
Tenants          ──→ (sin FK a catálogo)
EmailAccounts    ──→ EmailProviders
EmailTemplatePartials
EmailTemplates
ConfigApp
PoliticasPwd
```

## Nivel 2 — Dependen de Nivel 0 + Nivel 1

```
AppsModulos      ──→ Apps, Modulos
RolesPermisos    ──→ Roles, Permisos
RolesPoliticasPwd──→ Roles, PoliticasPwd
ConfigTenants    ──→ Tenants
DominiosTenant   ──→ Tenants
ConfProvIden     ──→ ProvIden, Tenants
Accesos          ──→ Usuarios, Tenants, Apps, Roles
Usuarios         ──→ Tenants, EstadosUsr
HistorialPwd     ──→ Usuarios, TiposCambioPwd
Grupos           ──→ Tenants
GruposUsuarios   ──→ Grupos, Usuarios
UsuariosPermisos ──→ Usuarios, Permisos, TipAsigPermiso
Sesiones         ──→ Usuarios, Apps
TokensRest       ──→ Usuarios
IntentosAcceso   ──→ Usuarios, ResultadosAcceso
Bloqueos         ──→ Usuarios, TiposBloqueo, Apps
MFA              ──→ Usuarios, TiposMFA, EstadosMFA
Disp             ──→ Usuarios, TiposDisp
DispConfiables   ──→ Disp
IPs              ──→ Usuarios
UserAgents       ──→ Usuarios
IdenExt          ──→ Usuarios, ProvIden, EstIdenExt
IdenExtTokens    ──→ IdenExt
HistorialIdenExt ──→ IdenExt, TiposAuditoria
AudIdenExt       ──→ Usuarios, ProvIden, TiposAuditoria, ResultadosAcceso
AuditoriaPwd     ──→ Usuarios, TiposAuditoria
Notificaciones   ──→ Usuarios, TiposAuditoria
EmailLog         ──→ Usuarios, EmailAccounts, EmailTemplates
EmailTemplateHistorial ──→ EmailTemplates
TenantEmailAccounts    ──→ Tenants, EmailAccounts
AppEmailAccounts       ──→ Apps, EmailAccounts
RolesHerencia    ──→ Roles
```

---

## Árbol de dependencias completo

```
N0 (raíz, 14)
├── EstadosUsr, EstadosMFA, EstIdenExt
├── TiposMFA, TiposDisp, TiposCambioPwd, TiposBloqueo, TiposAuditoria, TipAsigPermiso
├── TiposModulo, ResultadosAcceso
├── ProvIden, EmailProviders, Apps

N1 (dependen solo de N0, 9)
├── Modulos ──→ TiposModulo
├── Permisos ──→ Modulos
├── Roles
├── Tenants
├── EmailAccounts ──→ EmailProviders
├── EmailTemplatePartials
├── EmailTemplates
├── ConfigApp
├── PoliticasPwd

N2 (dependen de N0+N1, 32)
├── AppsModulos ──→ Apps, Modulos
├── RolesPermisos ──→ Roles, Permisos
├── RolesPoliticasPwd ──→ Roles, PoliticasPwd
├── ConfigTenants ──→ Tenants
├── DominiosTenant ──→ Tenants
├── ConfProvIden ──→ ProvIden, Tenants
├── Usuarios ──→ Tenants, EstadosUsr
├── Accesos ──→ Usuarios, Tenants, Apps, Roles
├── HistorialPwd ──→ Usuarios, TiposCambioPwd
├── Grupos ──→ Tenants
├── GruposUsuarios ──→ Grupos, Usuarios
├── UsuariosPermisos ──→ Usuarios, Permisos, TipAsigPermiso
├── Sesiones ──→ Usuarios, Apps
├── TokensRest ──→ Usuarios
├── IntentosAcceso ──→ Usuarios, ResultadosAcceso
├── Bloqueos ──→ Usuarios, TiposBloqueo, Apps
├── MFA ──→ Usuarios, TiposMFA, EstadosMFA
├── Disp ──→ Usuarios, TiposDisp
├── DispConfiables ──→ Disp
├── IPs ──→ Usuarios
├── UserAgents ──→ Usuarios
├── IdenExt ──→ Usuarios, ProvIden, EstIdenExt
├── IdenExtTokens ──→ IdenExt
├── HistorialIdenExt ──→ IdenExt, TiposAuditoria
├── AudIdenExt ──→ Usuarios, ProvIden, TiposAuditoria, ResultadosAcceso
├── AuditoriaPwd ──→ Usuarios, TiposAuditoria
├── Notificaciones ──→ Usuarios, TiposAuditoria
├── EmailLog ──→ Usuarios, EmailAccounts, EmailTemplates
├── EmailTemplateHistorial ──→ EmailTemplates
├── TenantEmailAccounts ──→ Tenants, EmailAccounts
├── AppEmailAccounts ──→ Apps, EmailAccounts
├── RolesHerencia ──→ Roles
```

---

## Orden seguro para DELETE / RESET (inverso del grafo)

```
FASE 1: EmailTemplateHistorial, EmailLog, AudIdenExt, HistorialIdenExt, IdenExtTokens
FASE 2: Notificaciones, AuditoriaPwd, UsuariosPermisos
FASE 3: GruposUsuarios, Accesos, RolesHerencia, RolesPoliticasPwd, RolesPermisos
FASE 4: HistorialPwd, Sesiones, TokensRest, IntentosAcceso, Bloqueos, MFA, DispConfiables, Disp, IPs, UserAgents
FASE 5: IdenExt, Grupos
FASE 6: ConfProvIden, EmailAccounts (solo operacional), AppEmailAccounts, TenantEmailAccounts
FASE 7: Usuarios (no admin)
FASE 8: Tenants (solo no sistema), Roles (solo no globales), Permisos, Modulos
FASE 9: EmailTemplates, EmailTemplatePartials
```

## Orden seguro para INSERT / SEED (dirección del grafo)

```
FASE 1: Catálogos (14) — N0
FASE 2: Modulos, Permisos, Roles globales, Tenants, ConfigApp, PoliticasPwd — N1
FASE 3: EmailProviders, EmailAccounts, EmailTemplates, EmailTemplatePartials — N1
FASE 4: AppsModulos, RolesPermisos, ConfigTenants, DominiosTenant — N2
FASE 5: Usuarios, ConfProvIden — N2
FASE 6: HistorialPwd, Accesos, Grupos — N2
FASE 7: GruposUsuarios, Sesiones, TokensRest, etc. — N2 (operacional)

NOTA: Catálogos y configuración se insertan en orden inverso al DELETE.
Operacional se genera durante ejecución, no en seed.
```

---

## Tablas que NUNCA deben eliminarse en RESET

| Tabla | Motivo |
|-------|--------|
| EstadosUsr | Catálogo base |
| Todos los catálogos (14) | No son operacionales |
| Modulos | Configuración plataforma |
| Permisos | Configuración seguridad |
| Roles (globales) | Configuración plataforma |
| Tenants (sistema) | El tenant PLATFORM es permanente |
| Usuarios (sistema) | Usuario sistema Id=1 |
| Usuarios (platform_admin) | Admin funcional Id=2 |
| ConfProvIden | Configuración OAuth |
| PoliticasPwd | Configuración seguridad |
| ConfigApp | Configuración global |
| EmailProviders | Catálogo |
| EmailAccounts (SMTP) | Configuración email |
| ProvIden | Catálogo OAuth |
| Apps | Catálogo aplicaciones |
