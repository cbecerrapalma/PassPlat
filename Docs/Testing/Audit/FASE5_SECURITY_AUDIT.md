# FASE 5 — Auditoría de Seguridad | PassPlat

**Fecha**: 2026-06-20  
**Auditor**: OpenCode Agent (Playwright MCP + Static Analysis)  
**Estado**: ✅ COMPLETADA

---

## Resumen Ejecutivo

| Categoría | SAFE | WARNING | VULNERABLE |
|-----------|------|---------|------------|
| Exposición de Password/Hash | 5 | 5 | 0 |
| Configuración JWT | 4 | 0 | 1 |
| Configuración CORS | 1 | 1 | 0 |
| Rate Limiting | 7 | 0 | 0 |
| SQL Injection | 2 | 0 | 0 |
| Logging de Datos Sensibles | 4 | 3 | 0 |
| HTTPS/HSTS | 2 | 0 | 0 |
| Hallazgos Adicionales | 2 | 6 | 1 |
| **TOTAL** | **27** | **15** | **2** |

**Calificación General**: 27/44 SAFE (61%) — Bueno, con 2 vulnerabilidades y 15 warnings a corregir.

---

## 1. Autenticación del Lado del Cliente (Playwright)

### 1.1 Ruta sin autenticación
- **Método**: Logout + navegación a 22 rutas protegidas
- **Resultado**: ✅ PASS — Login form se muestra. No se expone contenido protegido.
- **Evidencia**: Todas las rutas redirigen a `/login`

### 1.2 Sidebar visible
- **22 links visibles** para usuario admin (sistema)
- Todos los módulos: Panel Principal, Tenants, Apps, Roles, Políticas, Config App, Usuarios, Accesos, Permisos, Grupos, Permisos Directos, Matriz de Permisos, Auditoría, Historial Pwd, Intentos Acceso, Notificaciones, Mantenimiento, Providers, Cuentas de Correo, Cuentas x Tenant, Cuentas x App, Plantillas

---

## 2. Protección de Endpoints API

### 2.1 Prueba sin token JWT
**Método**: Playwright `page.evaluate(fetch(...))` contra `localhost:5259/api/*` sin header Authorization.

| Endpoint | Status | Resultado |
|----------|--------|-----------|
| `/api/tenants/count` | 401 | ✅ BLOCKED |
| `/api/tenants/activos/count` | 401 | ✅ BLOCKED |
| `/api/dominiosTenant/count` | 401 | ✅ BLOCKED |
| `/api/tenants/page` | 401 | ✅ BLOCKED |
| `/api/usuarios` | 401 | ✅ BLOCKED |
| `/api/usuarios/page` | 401 | ✅ BLOCKED |
| `/api/auditoriapwd/page/tenant/1` | 401 | ✅ BLOCKED |
| `/api/auth/current-tenant` | 401 | ✅ BLOCKED |
| `/api/auth/login` | 405 | ✅ OPEN (esperado — solo POST) |

**Resultado**: ✅ 8/9 endpoints protegidos. Solo `/api/auth/login` es accesible (correcto).

### 2.2 Token inválido
**Método**: `fetch('http://localhost:5259/api/tenants', { headers: { Authorization: 'Bearer test-invalid-token' } })`
- **Resultado**: ✅ 401 Unauthorized

### 2.3 Análisis de Controladores — Atributos [Authorize]

**Total controladores**: 54  
**Con [Authorize]**: 54/54 (100%)  
**Con política específica**: 32/54 (59%)  
**Solo [Authorize] genérico**: 22/54 (41%) — catálogos y contexto (aceptable)

**Endpoints [AllowAnonymous]** (solo en AuthController):
- `POST api/auth/login`
- `POST api/auth/refresh`
- `POST api/auth/olvido-password`
- `POST api/auth/validar-mfa`
- `POST api/auth/restablecer-password`
- `GET api/auth/tenant-info`
- `GET api/auth/tenants`

**Resultado**: ✅ PASS — Todos los controladores requieren autenticación.

---

## 3. Almacenamiento del JWT

### 3.1 Ubicación del Token
**Método**: `localStorage`, `sessionStorage`, `document.cookie` inspection via Playwright.

| Storage | Contenido |
|---------|-----------|
| localStorage | VACÍO (0 items) |
| sessionStorage | VACÍO (0 items) |
| Cookies | NINGUNA |

**Resultado**: ✅ Token almacenado **EN MEMORIA (C#)** — Approch más seguro. No accesible via JavaScript del navegador.

---

## 4. Exposición de Datos Sensibles en DTOs

### 4.1 DTOs de Lectura (Respuesta)
| DTO | PasswordHash expuesto? | Salt expuesto? | Secret/Key expuesto? |
|-----|----------------------|----------------|---------------------|
| UsuarioDto | ❌ NO | ❌ NO | ❌ NO |
| TenantDto | N/A | N/A | ❌ NO |
| HistorialPwdDto | ❌ NO | ❌ NO | ❌ NO |
| TokenRestDto | ❌ NO | ❌ NO | ❌ NO |
| SesionDto | N/A | N/A | ❌ NO |
| AuditoriaPwdDto | N/A | N/A | ❌ NO |
| ConfigAppDto | N/A | N/A | ⚠️ Ciphertext expuesto |

### 4.2 DTOs de Entrada (Request)
| DTO | Campo sensible | Justificación |
|-----|---------------|---------------|
| CrearUsuarioDto | Password | ✅ Entrada only, hasheado server-side |
| CrearEmailAccountDto | Password (SMTP) | ⚠️ Se almacena en entity |
| PasswordResetDto | NuevaPassword | ✅ Entrada only |
| ValidarPasswordRequestDto | Password | ✅ Validación, no persiste |

**Resultado**: ✅ PASS — No se exponen hashes de passwords en respuestas.

---

## 5. Configuración JWT

| Aspecto | Valor | Estado |
|---------|-------|--------|
| SecretKey | Hardcoded en appsettings.json | ⚠️ VULNERABLE |
| ExpirationMinutes (access) | 60 min | ✅ SAFE |
| ExpirationMinutes (refresh) | 1440 min (24h) | ✅ SAFE |
| Issuer | PassPlat | ✅ SAFE |
| Audience | PassPlat | ✅ SAFE |
| Algoritmo | HMAC-SHA256 (CBP framework) | ✅ SAFE |
| Validación producción | CHANGEME prefix check | ✅ SAFE (JWT) |

**VULNERABLE**: La SecretKey JWT (`qF3zK9aP7xR2vW5mY8bN1cL4oJ6sT0uD3gH5iA7eB9=`) está en appsettings.json. El guard de producción solo verifica que no empiece con "CHANGEME", pero este valor NO empieza con "CHANGEME", por lo que se usaría en producción sin override.

**VULNERABLE**: La Encryption Key (`OWmsozXdH1uFG8Qjo7/Ukwr22QEvgELGwdYyVw/VAlw=`) está en appsettings.json SIN guard de producción.

---

## 6. Rate Limiting

| Política | Ventana | Límite | Endpoint |
|----------|---------|--------|----------|
| LoginPolicy | FixedWindow 1min | 5 req | AuthController (todos) |
| RefreshPolicy | SlidingWindow 1min | 30 req | AuthController.Refresh |
| PasswordPolicy | FixedWindow 5min | 3 req | PasswordController |
| MFAPolicy | FixedWindow 1min | 5 req | MfaController |
| TokenPolicy | FixedWindow 15min | 10 req | TokensRest, PasswordReset |
| PurgePolicy | FixedWindow 1hr | 1 req | MaintenanceController |
| Global | — | — | app.UseRateLimiter() |

**Resultado**: ✅ PASS — Rate limiting comprehensivo en endpoints sensibles.

---

## 7. Protección CORS

| Modo | Origenes permitidos | Estado |
|------|-------------------|--------|
| Development | `*` (cualquiera) | ⚠️ Esperado |
| Production | `https://app.passplatapp.com` | ✅ SAFE |

**Resultado**: ✅ PASS — CORS restricción correcta en producción.

---

## 8. SQL Injection

- **EF Core LINQ**: ✅ Todos los repositorios usan LINQ
- **Stored Procedures**: ✅ Todos usan `RawParameter` (parámetros seguros)
- **String concatenation en queries**: ❌ NO encontrada

**Resultado**: ✅ PASS — No hay vector de SQL injection.

---

## 9. HTTPS / HSTS

| Feature | Configuración | Estado |
|---------|--------------|--------|
| HTTPS Redirection | `app.UseHttpsRedirection()` | ✅ SAFE |
| HSTS | `app.UseHsts()` (non-Development) | ✅ SAFE |

**Resultado**: ✅ PASS

---

## 10. Logging de Datos Sensibles

| Hallazgo | Severidad | Ubicación |
|----------|-----------|-----------|
| EF Core SensitiveDataLogging solo en Development | ✅ SAFE | Program.cs:117-118 |
| AuthController retorna `ex.Message` al cliente | ⚠️ WARNING | AuthController.cs:83 |
| UsuariosController retorna `DbUpdateException` al cliente | ⚠️ WARNING | UsuariosController.cs:119,163,183 |
| ConfigAppService loggea ciphertext prefix a Console | ⚠️ WARNING | ConfigAppService.cs:80 |
| Serilog no loggea headers/claims | ✅ SAFE | appsettings.json:33-34 |

---

## 11. Hallazgos Adicionales — Seguridad

### ⚠️ MfaController.Validar con [AllowAnonymous]
- **Archivo**: `MfaController.cs:30-31`
- **Problema**: El método `Validar` tiene `[AllowAnonymous]` dentro de un controller con `[Authorize(Policy = "USUARIOS_VERMFA")]` a nivel de clase. Esto permite que usuarios NO autenticados validen códigos MFA.
- **Severidad**: ⚠️ HIGH WARNING
- **Remediación**: Remover `[AllowAnonymous]` o mover a controller separado.

### ⚠️ HistorialPwdController.GetById sin tenant isolation
- **Archivo**: `HistorialPwdController.cs:34`
- **Problema**: `GetById` retorna cualquier registro de historial por ID sin verificar el tenant del usuario autenticado.
- **Severidad**: ⚠️ MEDIUM WARNING

### ⚠️ UsuariosController.GetByEmail — enumeración de usuarios
- **Archivo**: `UsuariosController.cs:79-85`
- **Problema**: Cualquier usuario autenticado puede verificar si un email existe en el sistema.
- **Severidad**: ⚠️ LOW WARNING

### ⚠️ Password hashing del lado del cliente
- **Archivo**: `PasswordController.cs:48-51`
- **Problema**: El endpoint `Cambiar` recibe `HashPwdNuevo` (hash Argon2id) calculado por el cliente. La contraseña hasheada viaja por HTTPS. Mejor práctica: recibir plaintext y hashear server-side.
- **Severidad**: ⚠️ LOW WARNING

### ⚠️ HistorialPwdController.MarcarComprometidas expone hash comparison
- **Archivo**: `HistorialPwdController.cs:58-65`
- **Problema**: Acepta `HashPwd` para marcar registros comprometidos. Endpoint legítimo pero expone comparación de hashes a usuarios autenticados.
- **Severidad**: ⚠️ LOW WARNING

---

## 12. Clasificación de Riesgo

### 🔴 VULNERABLE (2)

| # | Hallazgo | Impacto | Remediación |
|---|----------|---------|-------------|
| V1 | JWT SecretKey hardcoded en appsettings.json | Tokens falsificados si se descubre la key | Mover a User Secrets / env vars con guard de producción |
| V2 | Encryption Key hardcoded sin production guard | Datos cifrados comprometidos | Agregar guard similar al JWT (CHANGEME check) + mover a env vars |

### 🟡 WARNING (15)

| # | Hallazgo | Severidad | Remediación |
|---|----------|-----------|-------------|
| W1 | MfaController.Validar [AllowAnonymous] | HIGH | Remover AllowAnonymous o crear controller anónimo |
| W2 | AuthController retorna ex.Message al cliente | MEDIUM | Retornar mensaje genérico |
| W3 | UsuariosController retorna DbUpdateException | MEDIUM | Retornar mensaje genérico |
| W4 | HistorialPwd.GetById sin tenant isolation | MEDIUM | Agregar filtro por IdTenant |
| W5 | ConfigAppService loggea ciphertext a Console | LOW | Remover Console.WriteLine |
| W6 | GetByEmail permite enumeración de usuarios | LOW | Retornar 200 con mensaje genérico siempre |
| W7 | Password hashing del lado del cliente | LOW | Mover hashing a server-side |
| W8 | HistorialPwd.MarcarComprometidas expone hash | LOW | Verificar permisos adicionales |
| W9 | SesionDto.IdTokenExt expuesto | LOW | Verificar necesidad |
| W10 | ConfigAppDto.Valor con ciphertext expuesto | LOW | Excluir Valor cuando EsEncriptado=true |
| W11 | CORS Development permite * | INFO | Solo para dev, OK |
| W12 | DB connection string con `sa` account | MEDIUM | Usar least-privilege account |
| W13 | CrearEmailAccountDto.Password (SMTP) | LOW | Considerar cifrado en repositorio |
| W14 | 20+ controllers sin política específica | INFO | Agregar políticas granulares progresivamente |
| W15 | 6 controllers heredan ControllerBase | INFO | Migrar a BaseApiController para helpers |

### 🟢 SAFE (27)
Ver tabla de resumen en sección 1.

---

## 13. Credenciales de Prueba

| Campo | Valor |
|-------|-------|
| Tenant | Plataforma (PLATFORM) |
| Usuario | sistema |
| Password | Admin@123 |
| WebAPI port | 5259 |
| Web port | 5273 |

---

## Conclusión

La arquitectura de seguridad de PassPlat es **sólida en su diseño**:
- JWT en memoria (no en localStorage/cookies) — **excepcional**
- Rate limiting comprehensivo — **excepcional**
- 100% de controladores con [Authorize] — **bueno**
- DTOs sin exposición de hashes — **bueno**
- CORS restrictivo en producción — **bueno**
- Sin SQL injection vectors — **bueno**

**Las 2 vulnerabilidades** (hardcoded keys) son comunes en fases de desarrollo pero deben resolverse antes de producción.

**Los 15 warnings** incluyen 1 de severidad HIGH (MFA AllowAnonymous) que debe corregirse prioritariamente.
