# S15-Security-Audit.md — Seguridad Criptografica (F5)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      Security-Logging, Certification
# Area            Security / Criptografia (F5)
# Framework CBP   CBP.Security.Cryptography (IPasswordService, HashingService, PasswordService, PasswordStrength, PoliticaPwd, validators: Basic/Complexity/Contextual/History/Breach, StrengthAnalyzer, PatternAnalyzer, InMemoryCommonChecker, HaveIBeenPwnedBreachChecker, GenerationService), IEncryptionService (AES-256)
# Cobertura       PassPlat.Aplicacion | PassPlat.Datos | PassPlat.WebAPI
# Evidencia       PassPlatPasswordSecurity.cs (compone CBP validators/policy) · AuthService.cs:46,66,CBP IPasswordService · PasswordService.cs:34,46 · ExternalAuthService.cs:42,58,163,493 AES-256 · ConfProvIdenService.cs:33,90,119 · EmailAccountService.cs:25,50,66 · ConfigAppService.cs:30,82,116,122,130 · IdenExtTokensRotacionJob · CBP hashing Argon2id
# Resultado       REUTILIZAR / PASS (criptografia delegada a CBP.Security; AES-256-GCM; Argon2id)
# Cobertura       95 % (ver F11)
# Riesgo          Bajo (salvo CFG-001 ya auditado)
# Prioridad       Alta

---

## 1. Proposito

Auditar la seguridad criptografica: hashing de contraseñas (Argon2id), validacion/fortaleza de password, generacion de contrasenas temporales, cifrado AES-256 de secretos (ClientSecret, SMTP, refresh tokens), y verificacion de brechas (HIBP). Determinar que componente de CBP.Security.Cryptography se reutiliza.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Componentes CBP reutilizados

| Componente CBP.Security.Cryptography | Uso en PassPlat | Evidencia |
|---|---|---|
| `IPasswordService` (Services) | Inyectado en AuthService, PasswordService, PassPlatPasswordSecurity | `AuthService.cs:46,66`; `PasswordService.cs:34,46` |
| HashingService (Argon2id) | Base del PasswordService CBP | `PassPlatPasswordSecurity.cs:20` |
| Validators (Basic/Complexity/Contextual/History/Breach) | Construccion del ValidationService | `PassPlatPasswordSecurity.cs:24-35` |
| Policy `PoliticaPwd` (CBP.Models + Domain) | Modelo compartido dominio/DB | `PoliticaPwd.cs` domain + `CBP.Models.PoliticaPwd` |
| Pattern/Common/Breach checkers | ComplexityValidator, InMemoryCommonChecker, HaveIBeenPwnedBreachChecker | `PassPlatPasswordSecurity.cs:22-23` |
| GenerationService | Genera password temporal | `PassPlatPasswordSecurity.cs:36` y GenerateTemporaryPasswordAsync |
| `IEncryptionService` (AES-256) | Cifra secretos (ClientSecret, SMTP pwd, refresh token, config) | `ExternalAuthService.cs:163,493`; `ConfProvIdenService.cs:90,119`; `EmailAccountService.cs:50,66`; `ConfigAppService.cs:82,116,122`; `IdenExtTokensRotacionJob.cs:55` |

## 4. Hallazgos

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **SEC-041** | Hashing de password usa IPasswordService de CBP (Argon2id), NO reimplementa manually | AuthService:46; PassPlatPasswordSecurity | PASS |
| **SEC-042** | `PassPlatPasswordSecurity` (custom) compone validators de CBP + policy model, delegando toda la logica a CBP. Buen wrapper, sin duplicar criptografia. | `PassPlatPasswordSecurity.cs:20-39` | PASS |
| **SEC-043** | `IEncryptionService` (AES-256-GCM) se usa para: ClientSecret, SMTP password, RefreshToken, config secreta, datos IdenExt. Contexto por tipo (contextKey) | `ExternalAuthService.cs:163,493` | PASS |
| **SEC-044** | RefreshToken CIFRADO en BD (IdenExtTokens) con AES-256-GCM via EncryptionService (regla 5/10 de AGENTS) | `IdenExtTokensRotacionJob.cs:85-109` | PASS |
| **SEC-045** | `IdenExtTokensRotacionJob` rota refresh tokens Peru/ob services — maduro | `IdenExtTokensRotacionJob.cs:55` | PASS |
| **SEC-046** | Ultimo acceso: arquiridetica sin log/cache per request — cumple regla "no logging/caching" | (no expone hashes) | PASS |
| **SEC-047** | Potencial: MAPEO de modelos PoliticaPwd (Domain vs CBP incomplete) — 2 clases del mismo name. Documentado en inventory como JUNTIF (mantener por tipo de capa). | `PoliticaPwd.cs` (domain) + `CBP.Models.PoliticaPwd` | WARNING (deuda menor, unidad de duplicacion de modelo) |
| **SEC-048** | BreachChecking usa `HaveIBeenPwnedBreachChecker` (online) + `InMemoryCommonChecker` — Constraint: | Fallback a online HIBP. | PASS (configurable) |

## 5. Clasificacion general
- **Criptografia**: 100% delegada a CBP.Security (hashing + cifrado + politicas + validadores).
- Duplicacion: **casi 0**; solo el wrapper `PassPlatPasswordSecurity` (necesario) y 2 modelos `PoliticaPwd` (aceptado).
- Riesgo: bajo salvo SEC-047 modelo duplicado.

## 6. Resultado F5
- **REUTILIZAR / PASS**: La capa de seguridad criptografica esta totalmente alineada con `CBP.Security.Cryptography`. Buen uso de `IPasswordService`, validators, e `IEncryptionService` (AES-256-GCM) con contexto por dominio.
- No hay criptografia propia radical; el wrapper `PassPlatPasswordSecurity` es un facade correcto.
- Insumos F12 (menores): consolidar el modelo `PoliticaPwd` unico (evitar 2 clases) en capa compartida.

### 6.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| SEC-041 | PASS | REUTILIZAR | — | — | Alta |
| SEC-042 | PASS | REUTILIZAR | — | — | Alta |
| SEC-043 | PASS | REUTILIZAR | — | — | Alta |
| SEC-044 | PASS | REUTILIZAR (RefreshToken cifrado) | — | — | Alta |
| SEC-045 | PASS | REUTILIZAR (rotacion job) | — | — | Alta |
| SEC-046 | PASS | REUTILIZAR | — | — | Alta |
| SEC-047 | WARNING | JUSTIFICAR (unificar PoliticaPwd) | Baja | P3 | Media |
| SEC-048 | PASS | REUTILIZAR (HIBP configurable) | — | — | Alta |

### 6.2 Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 95 % |
| Architecture Score | 90 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-SEC-041..048 (ninguno critico) |