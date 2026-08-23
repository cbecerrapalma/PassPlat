# S15-Configuration-Audit.md — Configuration Audit (F9.3)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      Certification, Security
# Area            Configuracion (F9.3)
# Framework CBP   CBP.Logging (Serilog), CBP.Caching, CBP.Authentication.JwtBearer
# Cobertura       Aplicacion | Infraestructura | WebApi | Blazor | Workers
# Evidencia       appsettings.json/Development (WebAPI) · appsettings Blazor · 6 Options classes · Program.cs config wiring · ConfigAppService.cs:83
# Resultado       WARNING (con 1 hallazgo CRITICO de seguridad)
# Cobertura       78 % (ver F11)
# Riesgo          Alto (fuga ciphertext + credencial en texto plano)
# Prioridad       Muy Alta

---

## 1. Proposito

Auditar el modelo de configuracion: IConfiguration, Options (IOptions/IOptionsMonitor/IOptionsSnapshot), config por tenant, secretos, claves cifradas, config duplicada, constantes hardcodeadas, appsettings, variables de entorno, User Secrets y Logging. **Area con mayor enfasis del sprint** (decision del usuario).

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Origenes de configuracion detectados

| Origen | Uso en PassPlat | Archivo |
|---|---|---|
| appsettings.json | Logging, Serilog, Cbp.Logging, Jwt, Encryption, PasswordExpiration, Mfa, OAuth, OAuthMaintenance, ConnectionStrings | PassPlat.WebAPI/appsettings.json |
| appsettings.Development.json | ConnectionStrings (SA pwd), LogLevel | PassPlat.WebAPI/appsettings.Development.json |
| User Secrets | **NO detectado** (sin Secret Manager config) | — |
| Variables de entorno | Pepper (Consola), BLAZOR_BASE_URL (ExternalAuthController:328) | Consola, WebAPI |
| IConfiguration (host) | Program.cs:52,132,144,180-222 | WebAPI |
| Configuracion en BD | ConfigApp/ConfigTenant/EmailProviders/ConfProvIden (config de negocio dinámica) | BD |
| appsettings Blazor | ApiBaseUrl, AppSettings.AppId | PassPlat.Web/wwwroot/appsettings.json |

## 4. Clases Options (6)

| Class | File | Seccion | Inyectado como |
|---|---|---|---|
| `MfaOptions` | `Aplicacion/Options/MfaOptions.cs` | `Mfa` | IOptions<MfaOptions> (AuthService:52,72) |
| `OAuthOptions` | `Aplicacion/Options/OAuthOptions.cs` | `OAuth` | IOptions (external providers) |
| `OAuthMaintenanceOptions` | `Aplicacion/Options/OAuthMaintenanceOptions.cs` | `OAuthMaintenance` | IOptions (JwksStore:32) |
| `PasswordExpirationOptions` | `Services/Security/PasswordExpirationBackgroundService.cs:14` | `PasswordExpiration` | IOptions (BackgroundService:38) |
| `AppSettings` (Blazor) | `Web/Program.cs:13` | `AppSettings` | manual `Get<AppSettings>()` |
| `CbpLoggingOptions` (CBP) | CBP.Logging | `Cbp:Logging` | AddCbpLogging |

**Observacion**: Ningun servicio usa `IOptionsMonitor` ni `IOptionsSnapshot`. Todo es `IOptions` (snapshot estatica por request de DI). Para config que rota (JWKS TTL, email), `IOptionsMonitor` es lo correcto. Ver F12.

## 5. Hallazgos

### 5.1 Hallazgo CRITICO — fuga de ciphertext en logs/consola

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **CFG-001** | `Console.WriteLine` expone el **prefix del ciphertext** (40 chars) de valores encriptados de ConfigApp al crear una config encriptada. Esto fuga material cifrado (AES-256-GCM) a la consola/sink de logs. **Violacion directa de la regla "no exponer secrets/JWT/refresh/ciphertext"** | `PassPlat.Aplicacion/Services/BBDD/ConfigAppService.cs:83` | **FAIL - CRITICO** |

Impacto: un atacante con acceso a logs puede obtener fragmentos de ciphertext (40 chars) — facilita criptoanalisis y expone el patron de datos. Ademas es un `Console.WriteLine` directo que rompe observabilidad estructurada (no pasa por ILogger).

### 5.2 Secretos / credenciales

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **CFG-002** | Contrasena de SQL Server `inicio123` **en texto plano** en `appsettings.Development.json`. Se expone en repos a cualquier desarrollador con acceso al codigo. | `appsettings.Development.json` -> ConnectionStrings.PassPlatDb = `User Id=sa;Password=inicio123` | FAIL - CRITICO |
| **CFG-003** | `Jwt:SecretKey` y `Encryption:Key` en blanco en appsettings (deben venir de User Secrets/env/KeyVault). Correcto el patron de no commitear, pero no hay documentado el mecanismo de carga ni validacion de fallback (Program.cs:52 usa `GetSection("Jwt")`; la validacion de Encryption:Key L193-195 si existe) | `appsettings.json` (`"SecretKey": "", "Key": ""`) | JUSTIFICAR (patron ok, documentar) |
| **CFG-004** | Pepper (password pre-hashing) solo via env var `PEPPER` (Consola). En WebAPI no se configura pepper explicitamente para Argon2id. | `Consola/Program.cs:12,151` | WARNING (consistencia) |

### 5.3 Configuracion duplicada / redundante

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **CFG-005** | **Doble logging config**: `Serilog` (seccion propia) + `Cbp:Logging` (CBP.Logging) + `Logging` (ASP.NET). Tres modelos de logging configurados en paralelo. No hay un unico contrato. | `appsettings.json` (Serilog, Cbp:Logging, Logging) | WARNING |
| **CFG-006** | `Encryption:Key` en appsettings.json + validacion en Program.cs:193; duplicado de gestion de secretos entre appsettings y User Secrets. | `appsettings.json` + Program.cs:187-198 | JUSTIFICAR |
| **CFG-007** | Conexion DB definida en appsettings.json (vacia) Y en appsettings.Development.json (con credencial) — hereda, correcto; pero bin/Debug contiene copias de ambos (artefactos de build con credenciales en texto) | `bin\Debug\net10.0\appsettings*.json` | WARNING |

### 5.4 Constantes hardcodeadas (revisar en F1/F7)

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **CFG-008** | `AppId=1` hardcodeado en appsettings Blazor (AppSettings.AppId); Web/Program.cs:13 tiene `?? new AppSettings { AppId = 1 }` (fallback duplicado) | `PassPlat.Web/wwwroot/appsettings.json` + `Web/Program.cs:13` | WARNING |
| **CFG-009** | Puertos OAuth en launchSettings (7275/5273/5001/5259) — contrato ya documentado en AGENTS.md regla 22 | `WebAPI/launchSettings.json`, `Web/launchSettings.json` | PASS (documentado) |
| **CFG-010** | `EnableSensitiveDataLogging()` + `LogTo(Console.WriteLine, Information)` en Program.cs:133-138 para Development — correcto solo en dev, pero `Console.WriteLine` a nivel Informacion de EF puede capturar SQL con parametros en texto (passwords) incluso en dev | `Program.cs:133-138` | WARNING (solo dev) |

### 5.5 Configuracion por tenant (multi-tenancy)

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **CFG-011** | La config dinamica por tenant vive en BD (ConfigTenant, EmailProviders, ConfProvIden, TenantEmailAccounts) — modelo correcto y NO depende de appsettings. Aislado del tenant. | BD (modelo A1) | PASS |
| **CFG-012** | `Cbp:Logging` no permite config por tenant; el log de tenant se resuelve via middleware de correlacion (ver F7.1). Sin enricher de tenant actualmente. | appsettings Cbp | WARNING (falta TenantId enricher) |

### 5.6 Uso de IOptions/IOptionsMonitor/Snapshot

| Tipo | Usos | Evaluacion |
|---|---|---|
| IOptions<T> | 6 classes, ~8 inyecciones | OK para config estatica |
| IOptionsMonitor<T> | 1 (CbpForbidHandler) | OK (auth scheme options) |
| IOptionsSnapshot<T> | 0 | No usado |

Config que **deberia** usar IOptionsMonitor: OAuthMaintenance (JwksCacheTtl rota en runtime), EmailProviders (from BD no aplica). Ver F12.

## 6. Secretos — recomendaciones de mecanismo

| Secreto | Hoy | Deberia | Prioridad |
|---|---|---|---|
| SQL Password | appsettings.Development.json (texto) | User Secrets + env override | Muy Alta |
| Jwt:SecretKey | vacio (User Secrets manual) | User Secrets + KeyVault | Alta |
| Encryption:Key | vacio (User Secrets manual) | User Secrets + KeyVault | Alta |
| OAuth ClientSecret | BD cifrada AES-256-GCM (IdenExt/ConfProvIden) | OK (ya cifrado) | PASS |
| SMTP passwords | BD cifrada (EmailAccounts) | OK | PASS |
| Pepper | env var (Consola) | env var WebAPI | Media |

## 7. Observabilidad de configuracion (relacion con F7.1)
La configuracion no emite eventos de correlacion. Ver F7.1 para CorrelationId/TenantId en logs.

## 8. Resultado F9.3
- **FAIL critico**: CFG-001 (fuga ciphertext) y CFG-002 (credencial SQL texto plano).
- **WARNING**: triple config logging, hardcode AppId, sensitive data logging dev.
- **Bien**: secretos OAuth/SMTP cifrados en BD, config tenant dinamica en BD, validacion Encryption:Key en startup.

Insumo F12 → acciones y trazabilidad migradas a `S15-CBP-Refactoring-Plan.md` (Nivel 3). Este doc conserva SOLO evidencia N1.

### 8.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| CFG-001 | FAIL | REEMPLAZAR (eliminar fuga) | **Critica** | **P0** | Alta |
| CFG-002 | FAIL | REEMPLAZAR (User Secrets) | **Critica** | **P0** | Alta |
| CFG-003 | PASS CON OBS | JUSTIFICAR (documentar) | Media | P2 | Alta |
| CFG-004 | WARNING | DIFERIR | Media | P2 | Media |
| CFG-005 | WARNING | REEMPLAZAR (unificar contrato) | Media | P2 | Alta |
| CFG-007 | WARNING | ELIMINAR artefactos bin | Baja | P3 | Media |
| CFG-008 | WARNING | JUSTIFICAR (no hardcodear) | Baja | P3 | Alta |
| CFG-010 | WARNING | JUSTIFICAR (solo dev) | Media | P2 | Alta |
| CFG-012 | WARNING | EXTENDER (enricher TenantId) | Media | P2 | Media |

## 9. Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 78 % (config en BD + IOptions parcial) |
| Architecture Score | 58 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-CFG-001..012 |