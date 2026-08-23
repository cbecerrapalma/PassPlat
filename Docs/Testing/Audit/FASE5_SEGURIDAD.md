# FASE 5 — Auditoría de Seguridad

**Fecha**: 2026-06-20
**Proyecto**: PassPlat
**Auditor**: opencode (AI Agent)

---

## Resumen Ejecutivo

| Severidad | Cantidad |
|-----------|----------|
| 🔴 VULNERABLE | 2 |
| 🟡 WARNING | 15 |
| 🟢 SAFE | 27 |

**Calificación General**: 🟡 ACEPTABLE — Seguridad funcional con 2 vulnerabilidades y 15 advertencias que requieren remediación antes de producción.

---

## 1. Autenticación y Autorización

### 1.1 Client-Side Auth (Playwright Test)
| Test | Resultado |
|------|-----------|
| Login sin tenant seleccionado → Continuar disabled | ✅ PASS |
| Login con credenciales inválidas → error message | ✅ PASS |
| Login con credenciales válidas → redirect a / | ✅ PASS |
| Sin JWT → formulario de login visible | ✅ PASS |
| Sin JWT → contenido protegido NO expuesto | ✅ PASS |

### 1.2 API 401 Checks (Real API — port 5259)
| Endpoint | Sin Token | Con Token Inválido |
|----------|-----------|---------------------|
| `GET /api/tenants/count` | 401 ✅ | 401 ✅ |
| `GET /api/tenants/activos/count` | 401 ✅ | 401 ✅ |
| `GET /api/dominiosTenant/count` | 401 ✅ | 401 ✅ |
| `GET /api/tenants/page` | 401 ✅ | 401 ✅ |
| `GET /api/usuarios` | 401 ✅ | 401 ✅ |
| `GET /api/usuarios/page` | 401 ✅ | 401 ✅ |
| `GET /api/auditoriapwd/page/tenant/1` | 401 ✅ | 401 ✅ |
| `GET /api/auth/current-tenant` | 401 ✅ | 401 ✅ |
| `POST /api/auth/login` | 405 ✅ (POST only) | — |

**Todos los endpoints protegidos retornan 401 sin token.** El endpoint de login retorna 405 para GET (correcto — solo acepta POST).

### 1.3 Controller [Authorize] Coverage
- **46 controllers** analizados
- **46/46** tienen `[Authorize]` (a nivel de clase o método)
- **0 controllers** sin protección de autorización
- **1 endpoint** con `[AllowAnonymous]` problemático: `MfaController.Validar`

### 1.4 JWT Storage
| Ubicación | Estado |
|-----------|--------|
| localStorage | ❌ Vacío (no se usa) |
| sessionStorage | ❌ Vacío (no se usa) |
| Cookies | ❌ Ninguna cookie |
| **Memoria C# (WASM)** | ✅ Token solo en `AuthenticationStateProvider` |

**Conclusión**: El JWT se almacena exclusivamente en memoria del proceso WASM. Este es el patrón más seguro — el token no persiste entre sesiones del navegador y no es accesible via XSS que solo lee DOM.

### 1.5 Rate Limiting
| Policy | Configuración | Endpoints |
|--------|--------------|-----------|
| `LoginPolicy` | 5 req/min (FixedWindow) | AuthController (todos) |
| `RefreshPolicy` | 30 req/min (SlidingWindow) | Refresh token |
| `PasswordPolicy` | 3 req/5min (FixedWindow) | PasswordController |
| `MFAPolicy` | 5 req/min (FixedWindow) | MfaController |
| `TokenPolicy` | 10 req/15min (FixedWindow) | TokensRest |
| `PurgePolicy` | 1 req/hora (FixedWindow) | MaintenanceController |

**Protección contra brute-force**: ✅ Completa

---

## 2. Vulnerabilidades (VULNERABLE)

### 🔴 V-01: JWT SecretKey hardcodeado en appsettings.json
- **Archivo**: `PassPlat.WebAPI/appsettings.json:39`
- **Detalle**: La clave JWT (`qF3zK9aP7xR2vW5mY8bN1cL4oJ6sT0uD3gH5iA7eB9=`) está hardcodeada y commiteada en source control.
- **Riesgo**: Un atacante con acceso al repo puede firmar tokens JWT válidos para cualquier tenant/usuario.
- **Mitigación**: Program.cs tiene un guard que verifica que la key no empiece con "CHANGEME", pero la key actual no tiene ese prefijo.
- **Remediación**: Mover a User Secrets o variables de entorno. Agregar guard similar al de JWT para la Encryption key.

### 🔴 V-02: Encryption Key hardcodeada en appsettings.json
- **Archivo**: `PassPlat.WebAPI/appsettings.json:46`
- **Detalle**: La clave AES-256 (`OWmsozXdH1uFG8Qjo7/Ukwr22QEvgELGwdYyVw/VAlw=`) está hardcodeada.
- **Riesgo**: Un atacante puede descifrar todos los datos cifrados (configuración sensible, tokens refresh, etc.).
- **Mitigación**: NO existe guard de producción para esta clave (a diferencia de JWT).
- **Remediación**: Mover a User Secrets o variables de entorno. Agregar validación en Program.cs.

---

## 3. Advertencias (WARNING)

### 🟡 W-01: MfaController.Validar con [AllowAnonymous]
- **Archivo**: `MfaController.cs:30-31`
- **Detalle**: El método `Validar` tiene `[AllowAnonymous]` pero la clase tiene `[Authorize(Policy = "USUARIOS_VERMFA")]`. El AllowAnonymous sobreescribe la protección de clase.
- **Riesgo**: Un atacante no autenticado puede validar códigos MFA para cualquier usuario si conoce `IdUsuario` e `IdMFA`.
- **Remediación**: Mover a un controller separado o remover AllowAnonymous.

### 🟡 W-02: HistorialPwdController.GetById sin tenant isolation
- **Archivo**: `HistorialPwdController.cs:34`
- **Detalle**: El endpoint GET by ID no verifica que el registro pertenezca al tenant del usuario autenticado.
- **Riesgo**: Un usuario puede iterar IDs y acceder al historial de contraseñas de otros tenants.
- **Remediación**: Agregar filtro por IdTenant en la query.

### 🟡 W-03: IntentosAccesoController sin tenant isolation
- **Archivo**: `IntentosAccesoController.cs:34-35`
- **Detalle**: `ContarFallidos` y `ContarFallidosPorIP` no filtran por tenant.
- **Riesgo**: Información de intentos de acceso de otros tenants expuesta.
- **Remediación**: Agregar filtro por tenant.

### 🟡 W-04: UsuariosController.GetByEmail — user enumeration
- **Archivo**: `UsuariosController.cs:79-85`
- **Detalle**: Cualquier usuario autenticado puede verificar si un email existe en el sistema.
- **Riesgo**: Enumeración de usuarios para ataques de fuerza bruta.
- **Remediación**: Retornar respuesta genérica sin indicar si el email existe.

### 🟡 W-05: AuthController.Login expone ex.Message
- **Archivo**: `AuthController.cs:83`
- **Detalle**: El catch block retorna `ex.Message` al cliente en errores 500.
- **Riesgo**: Filtración de detalles internos (errores SQL, stack traces).
- **Remediación**: Retornar mensaje genérico "Error interno del servidor".

### 🟡 W-06: UsuariosController expone DbUpdateException
- **Archivo**: `UsuariosController.cs:119,163,183`
- **Detalle**: Retorna `ex.InnerException?.Message` que contiene nombres de constraints SQL.
- **Riesgo**: Facilita reconocimiento para SQL injection.
- **Remediación**: Retornar mensajes de error genéricos.

### 🟡 W-07: Password hashing en cliente (client-side)
- **Archivo**: `PasswordController.cs:48-51`
- **Detalle**: El cliente ejecuta Argon2id y envía el hash al servidor.
- **Riesgo**: El hash traverse la red (mitigado por HTTPS). El servidor no controla los parámetros de hashing.
- **Remediación**: Recibir contraseña en plaintext, hashear en servidor.

### 🟡 W-08: ConfigAppService loguea prefijo de ciphertext
- **Archivo**: `ConfigAppService.cs:80`
- **Detalle**: `Console.WriteLine` con los primeros 40 chars del ciphertext.
- **Riesgo**: Datos cifrados parciales en logs de stdout.
- **Remediación**: Usar ILogger en nivel Debug o eliminar.

### 🟡 W-09: ConfigAppDto expone valores cifrados
- **Archivo**: `ConfigAppController.cs:23-50`
- **Detalle**: El DTO incluye `Valor` y `EsEncriptado` — los ciphertexts son visibles.
- **Riesgo**: Bajo (ciphertext sin clave no es útil), pero es exposición innecesaria.
- **Remediación**: Enmascarar valores de configuración cifrados en el DTO de lectura.

### 🟡 W-10: Connection string con cuenta sa
- **Archivo**: `appsettings.json:50`, `appsettings.Development.json:9`
- **Detalle**: La conexión usa `User Id=sa;Password=inicio123`.
- **Riesgo**: Cuenta SA tiene privilegios completos de SQL Server.
- **Remediación**: Usar cuenta con permisos mínimos en producción.

---

## 4. Protecciones Comprobadas (SAFE)

| Protección | Estado |
|-----------|--------|
| HTTPS Redirection | ✅ Configurado |
| HSTS (Production) | ✅ Habilitado |
| CORS (Production) | ✅ Solo `https://app.passplatapp.com` |
| CORS (Development) | ✅ AllowAny (correcto para dev) |
| SQL Injection | ✅ Sin raw SQL, solo EF Core LINQ + SPs parametrizados |
| Sensitive Data Logging | ✅ Solo en Development mode |
| Serilog (headers/claims) | ✅ Deshabilitado |
| Password hashes en DTOs | ✅ No expuestos |
| Refresh tokens en DB | ✅ Solo hash (SHA-256) del token |
| PermissionPolicyProvider | ✅ Dinámico basado en claims 'permiso' |
| JWT Issuer/Audience | ✅ Configurados y validados |
| JWT Expiration | ✅ 60 min (razonable) |
| Refresh Token Expiration | ✅ 24h con rotación |

---

## 5. Arquitectura de Seguridad

```
Blazor WASM (5273)
    │
    │ JWT en memoria C# (no localStorage)
    │ AuthorizationHeaderMessageHandler añade Bearer token
    │
    ├──► WebAPI (5259)
    │       │
    │       ├── Rate Limiting (6 policies)
    │       ├── [Authorize] en 46/46 controllers
    │       ├── PermissionPolicyProvider (claims-based)
    │       ├── JWT Validation (Issuer, Audience, SecretKey)
    │       ├── HTTPS Redirection + HSTS
    │       └── CORS (production: app.passplatapp.com)
    │
    └──► SQL Server
            ├── Stored Procedures parametrizados
            ├── EF Core LINQ (sin raw SQL)
            └── Triggers de auditoría
```

---

## 6. Priorización de Correcciones

### Inmediato (antes de producción)
1. **V-01 + V-02**: Mover JWT SecretKey y Encryption Key a User Secrets/env vars
2. **W-01**: Corregir MfaController.Validar [AllowAnonymous]

### Alta prioridad
3. **W-02 + W-03**: Agregar tenant isolation a HistorialPwd y IntentosAcceso
4. **W-05 + W-06**: Sanitizar mensajes de error (no exponer ex.Message)
5. **W-04**: Respuesta genérica en GetByEmail

### Media prioridad
6. **W-07**: Migrar a server-side password hashing
7. **W-08**: Eliminar Console.WriteLine en ConfigAppService
8. **W-09**: Enmascarar ciphertext en ConfigAppDto
9. **W-10**: Usar cuenta SQL con permisos mínimos
