# PassPlat - FASE 13: EVOLUCIÓN DEL MODELO DE IDENTIDAD (USUARIOS SIN EMAIL)

## PLAN DE EJECUCIÓN COMPLETO

---

## 1. ANÁLISIS DE IMPACTO - MATRIZ DE DEPENDENCIAS

| Objeto | Dependencia | Tipo | Impacto |
|--------|-------------|------|---------|
| **BD: Usuarios** | `Email NOT NULL`, `UNIQUE (Email)` | Esquema | ALTO - Requiere migración ALTER TABLE |
| **BD: Usuarios** | `Trigger TR_Usuarios_Mod` (UPDATE Email) | Código SQL | MEDIO - Verificar compatibilidad |
| **BD: SP_Auth_Login** | `@Email nvarchar(255)` param, login por Email | SP | ALTO - Login debe funcionar sin Email |
| **BD: SP_Usuario_Crear** | `@Email nvarchar(255)` param, validación UNIQUE | SP | ALTO - Crear usuario sin Email |
| **Dominio: Usuario** | `Email string` requerido en `Crear()` | Código | ALTO - Factory method |
| **Dominio: Usuario** | `EmailVerificado` bool | Código | BAJO - Mantener, opcional |
| **DTO: CrearUsuarioDto** | `Email string` required | Contrato | ALTO - Hacer opcional |
| **DTO: UsuarioDto** | `Email string` (no nullable) | Contrato | MEDIO - Cambiar a `string?` |
| **DTO: ActualizarUsuarioDto** | Sin Email (ya opcional) | Contrato | BAJO - Sin cambios |
| **Validator: CrearUsuarioValidator** | `RuleFor(x => x.Email).NotEmpty()` | Validación | ALTO - Quitar NotEmpty, solo EmailAddress() when not null |
| **Validator: ActualizarUsuarioValidator** | Sin validación Email | Validación | BAJO - Sin cambios |
| **AuthService.LoginAsync** | Busca por `nomUsuario` OR `email` | Servicio | MEDIO - Ya soporta ambos, solo validar null |
| **AuthService.EnviarCodigoMfaAsync** | `if (metodoMfa.IdTipoMFA == Email)` | Servicio | ALTO - Solo encolar si Email existe |
| **AuthService.NotificarBloqueoAsync** | `if (string.IsNullOrWhiteSpace(usuario.Email)) return;` | Servicio | BAJO - Ya tiene guard |
| **AuthService.VerificarAlertaSeguridadAsync** | `if (string.IsNullOrWhiteSpace(usuario.Email)) return;` | Servicio | BAJO - Ya tiene guard |
| **PasswordExpirationBackgroundService** | Filtra `u.EmailVerificado` y usa `usuario.Email` | Servicio | ALTO - Solo usuarios con Email verificado |
| **PassPlatEmailService.SendFromJobAsync** | Resuelve Email desde usuario si job.ToEmail vacío | Servicio | MEDIO - Validar null antes de encolar |
| **PassPlatEmailService.IsValidEmailFormat** | Valida formato | Servicio | BAJO - Ya retorna false si null/empty |
| **EmailBackgroundService** | Procesa jobs, reintenta | Servicio | BAJO - No cambios |
| **PasswordService.NotificarCambioPasswordAsync** | `if (string.IsNullOrWhiteSpace(usuario.Email)) return;` | Servicio | BAJO - Ya tiene guard |
| **UsuarioService.CrearAsync** | Usa `Usuario.Crear(..., email, ...)` | Servicio | ALTO - Permitir email null |
| **UsuarioService.CrearConPasswordAsync** | SP requiere Email | Servicio | ALTO - SP debe permitir NULL |
| **UsuarioService.EnviarBienvenidaAsync** | `if (string.IsNullOrWhiteSpace(email)) return;` | Servicio | BAJO - Ya tiene guard |
| **AuthController.Login** | Acepta `nomUsuario` OR `email` | API | BAJO - Ya soporta ambos |
| **AuthController.OlvidoPassword** | Busca por Email obligatorio | API | ALTO - Flujo alternativo sin Email |
| **UsuariosController.Create** | Valida `CrearUsuarioDto` (Email required) | API | ALTO - DTO + Validator |
| **UsuariosController.Update** | No toca Email | API | BAJO - Sin cambios |
| **Blazor: UsuarioDialog** | Email Required, ErrorText "El email es requerido" | UI | ALTO - Hacer opcional |
| **Blazor: UsuarioGeneral** | Edita Email, no valida required | UI | MEDIO - Permitir vacío |
| **Blazor: PasswordStrength** | Usa Email para validación ProhInfoUsuario | UI | BAJO - Pasar null si no hay Email |
| **MFA: Email MFA** | `ETipoMFA.Email` envía código a Email | Funcionalidad | ALTO - Solo habilitar si Email existe |
| **EmailLog** | `IdUsuario`, `Destinatario` | Datos | BAJO - Solo loguear si hay Email |
| **EmailTemplates** | Variables `UserName`, `Email` | Plantillas | BAJO - Renderizar sin Email |

---

## 2. CAMBIOS EN BASE DE DATOS (FASE 1 - MODELO DE DATOS)

### 2.1 Script SQL de Migración

```sql
-- ============================================================
-- MIGRACIÓN: Permitir usuarios sin Email
-- ============================================================

-- 1. Eliminar índice único actual (no permite NULL duplicates en SQL Server)
DROP INDEX IF EXISTS UX_Usuarios_Email ON dbo.Usuarios;

-- 2. Hacer Email NULLable
ALTER TABLE dbo.Usuarios
ALTER COLUMN Email nvarchar(255) NULL;

-- 3. Crear índice único filtrado (solo para emails no nulos)
CREATE UNIQUE INDEX UX_Usuarios_TenantEmail
ON dbo.Usuarios (IdTenant, Email)
WHERE (Eliminado = 0 AND Email IS NOT NULL);

-- 4. Actualizar trigger TR_Usuarios_Mod para no fallar con Email NULL
-- (El trigger actual ya usa UPDATE(Email) que funciona con NULL)
-- Verificar: el trigger actual:
--   IF UPDATE(Nombre) OR UPDATE(Apellido) OR UPDATE(IdEstado) OR UPDATE(Email) OR UPDATE(IdTenant)
-- Esto funciona correctamente con NULL.

-- 5. Actualizar SP_Auth_Login para permitir login solo por NomUsuario
-- (Ver sección 2.2)

-- 6. Actualizar SP_Usuario_Crear para permitir Email NULL
-- (Ver sección 2.3)
```

### 2.2 SP_Auth_Login - Cambios Necesarios

```sql
-- El SP actual ya acepta @Email nvarchar(255) = NULL
-- La lógica de búsqueda ya es:
-- WHERE (u.NomUsuario=@NomUsuario OR u.Email=@Email)
-- Si @Email es NULL, la condición u.Email=@Email es UNKNOWN (falso en WHERE)
-- por lo que SOLO busca por NomUsuario.
-- NO REQUIERE CAMBIOS en la lógica de búsqueda.

-- PERO: El SP valida que al menos uno venga:
-- IF @NomUsuario IS NULL AND @Email IS NULL
--     RAISERROR('Debe especificar @NomUsuario o @Email.', 16, 1);
-- ESTO YA PERMITE solo NomUsuario. ✓
```

### 2.3 SP_Usuario_Crear - Cambios Necesarios

```sql
-- Verificar SP que crea usuarios (probablemente SP_Usuario_Crear o similar)
-- Debe:
-- 1. Aceptar @Email nvarchar(255) = NULL
-- 2. NO validar UNIQUE si Email IS NULL
-- 3. Insertar NULL en Email (no empty string)
```

---

## 3. CAMBIOS EN ENTIDADES DE DOMINIO (FASE 2 - DOMINIO)

### 3.1 Usuario.cs

```csharp
// Cambios en factory method
public static Usuario Crear(int idTenant, int idEstado, string nomUsuario, 
    string? email,  // CAMBIO: nullable
    string nombre, string apellido)
{
    return new Usuario
    {
        IdTenant = idTenant,
        IdEstado = idEstado,
        NomUsuario = nomUsuario,
        Email = email ?? string.Empty,  // Mantener string.Empty para EF, pero lógica de negocio usa null
        Nombre = nombre,
        Apellido = apellido,
        ReqCambioPwd = true,
        IntentosFallidos = 0,
        Eliminado = false,
        FecCrea = DateTime.UtcNow,
        FecMod = DateTime.UtcNow
    };
}

// Propiedad Email: cambiar a string? para lógica de negocio
// Pero mantener string en EF (ver configuración)
// OPCIONAL: Agregar propiedad computada
public string? EmailNullable => string.IsNullOrWhiteSpace(Email) ? null : Email;
public bool TieneEmail => !string.IsNullOrWhiteSpace(Email);
```

### 3.2 UsuarioConfiguration.cs

```csharp
// Cambiar IsRequired() a false
builder.Property(u => u.Email).HasMaxLength(255).IsRequired(false);

// Índice único filtrado (ya generado por migración, pero documentar)
builder.HasIndex(u => new { u.IdTenant, u.Email })
    .IsUnique()
    .HasFilter("Eliminado = 0 AND Email IS NOT NULL")
    .HasDatabaseName("UX_Usuarios_TenantEmail");
```

---

## 4. CAMBIOS EN DTOs (FASE 2 - DOMINIO)

### 4.1 CrearUsuarioDto.cs

```csharp
public class CrearUsuarioDto
{
    public int IdTenant { get; set; }
    public int IdEstado { get; set; }
    public int IdApp { get; set; }
    public string NomUsuario { get; set; } = string.Empty;
    public string? Email { get; set; }  // CAMBIO: nullable
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Password { get; set; }
}
```

### 4.2 UsuarioDto.cs

```csharp
public class UsuarioDto
{
    // ... otras propiedades
    public string? Email { get; set; }  // CAMBIO: nullable
    public bool EmailVerificado { get; set; }
    // ...
}
```

### 4.3 ActualizarUsuarioDto.cs

```csharp
// Ya tiene EmailVerificado como bool? - mantener
// No tiene Email - correcto (no se actualiza por este DTO)
```

---

## 5. CAMBIOS EN VALIDADORES (FASE 2 - DOMINIO)

### 5.1 CrearUsuarioValidator.cs

```csharp
public class CrearUsuarioValidator : AbstractValidator<CrearUsuarioDto>
{
    public CrearUsuarioValidator()
    {
        // ... otros rules

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
                .WithMessage("El email no es válido")
            .MaximumLength(255).When(x => !string.IsNullOrWhiteSpace(x.Email))
                .WithMessage("El email no puede exceder 255 caracteres");
        
        // QUITAR: .NotEmpty().WithMessage("El email es requerido")
    }
}
```

---

## 6. CAMBIOS EN SERVICIOS (FASE 3 - SERVICIOS)

### 6.1 AuthService.cs

```csharp
// En EnviarCodigoMfaAsync - YA tiene guard para Email MFA:
if (metodoMfa.IdTipoMFA != (int)ETipoMFA.Email) return;
// Solo envía si es Email MFA y usuario tiene Email

// En NotificarBloqueoAsync - YA tiene guard:
if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email)) return;

// En VerificarAlertaSeguridadAsync - YA tiene guard:
if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email)) return;

// AGREGAR: Validar en LoginConTokenAsync que si ReqCambioPwd y no hay Email,
// no se pueda recuperar password por email (flujo alternativo necesario)
```

### 6.2 PasswordExpirationBackgroundService.cs

```csharp
// En ObtenerUsuariosConExpiracionAsync:
// CAMBIAR: filtro actual: u.EmailVerificado
// A: u.EmailVerificado && !string.IsNullOrWhiteSpace(u.Email)
// Solo notificar expiración a usuarios CON Email verificado

foreach (var usuario in usuariosResult.Value
    .Where(u => !u.Eliminado && u.EmailVerificado && !string.IsNullOrWhiteSpace(u.Email)))
```

### 6.3 PassPlatEmailService.cs

```csharp
// En SendFromJobAsync - AGREGAR validación temprana:
if (string.IsNullOrWhiteSpace(resolvedEmail))
{
    _logger.LogInformation("Usuario {IdUsuario} sin Email configurado. No se genera EmailJob para {Kind}", 
        job.IdUsuario, job.Kind);
    return Result<EmailResult>.Failure("NO_EMAIL", "Usuario sin email configurado");
}

// En SendEmailAsync - IsValidEmailFormat YA retorna false para null/empty
// Pero agregar log informativo:
if (!IsValidEmailFormat(toEmail))
{
    _logger.LogWarning("Email inválido o vacío para {Kind}: {Email}", job.Kind, toEmail);
    return Result<EmailResult>.Failure("INVALID_EMAIL_FORMAT", $"Formato de email inválido o vacío: {toEmail}");
}
```

### 6.4 PasswordService.cs

```csharp
// NotificarCambioPasswordAsync - YA tiene guard:
if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email)) return;

// AGREGAR: Log informativo cuando no hay email
_logger.LogInformation("Usuario {IdUsuario} sin Email. No se envía notificación de cambio password.", idUsuario);
```

### 6.5 UsuarioService.cs

```csharp
// CrearAsync - Cambiar para permitir email null:
var usuario = Usuario.Crear(dto.IdTenant, dto.IdEstado, dto.NomUsuario, 
    dto.Email, dto.Nombre, dto.Apellido);

// EnviarBienvenidaAsync - YA tiene guard:
if (string.IsNullOrWhiteSpace(email)) return;

// CrearConPasswordAsync - El SP debe aceptar Email NULL (ver BD)
```

### 6.6 MfaService.cs (revisar)

```csharp
// Verificar que no se permita registrar MFA tipo Email si usuario no tiene Email
// En registrar MFA: validar que si IdTipoMFA == Email, usuario.Email no sea null
```

---

## 7. CAMBIOS EN CONTROLADORES/API (FASE 4 - API)

### 7.1 AuthController.cs

```csharp
// OlvidoPassword - CAMBIO MAYOR: Flujo alternativo sin Email
[AllowAnonymous]
[HttpPost("olvido-password")]
public async Task<IActionResult> OlvidoPassword([FromBody] SolicitarResetPasswordDto request, CancellationToken ct)
{
    // Si request.Email viene vacío/null, buscar por NomUsuario
    // Requerir al menos NomUsuario O Email
    
    if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.NomUsuario))
        return BadRequest(new { codigo = "IDENTIFICADOR_REQUERIDO", mensaje = "Debe proporcionar email o nombre de usuario" });
    
    var tenantId = request.IdTenant;
    var idApp = request.IdApp ?? 1;
    
    UsuarioDto? usuario = null;
    
    if (!string.IsNullOrWhiteSpace(request.Email))
    {
        var result = await _usuarioService.ObtenerPorEmailAsync(tenantId, request.Email, ct);
        if (result.IsSuccess) usuario = result.Value;
    }
    else if (!string.IsNullOrWhiteSpace(request.NomUsuario))
    {
        var result = await _usuarioService.ObtenerPorNomUsuarioAsync(tenantId, request.NomUsuario, ct);
        if (result.IsSuccess) usuario = result.Value;
    }
    
    if (usuario == null)
        return Ok(new PasswordResetResponseDto()); // No revelar si existe
    
    // Si usuario no tiene Email, NO se puede enviar token por email
    // FLUJO ALTERNATIVO: Generar token y devolverlo en respuesta (solo para admins/sistema)
    // O: Requerir que admin restablezca manualmente
    
    if (string.IsNullOrWhiteSpace(usuario.Email))
    {
        // Log informativo
        _logger.LogWarning("Recuperación de password solicitada para usuario sin Email: {NomUsuario}", usuario.NomUsuario);
        
        // OPCIÓN A: Devolver token en respuesta (solo si es admin/sistema)
        // OPCIÓN B: Requerir intervención de administrador
        // OPCIÓN C: Mostrar mensaje "Contacte a su administrador"
        
        return Ok(new PasswordResetResponseDto 
        { 
            RequiereAdmin = true, 
            Mensaje = "Este usuario no tiene email configurado. Contacte a su administrador para restablecer la contraseña." 
        });
    }
    
    // ... resto del flujo actual con email
}
```

### 7.2 UsuariosController.cs

```csharp
// Create - El validator ya permitirá Email null
// No requiere cambios adicionales en el controller
// El validator se ejecuta antes (FluentValidation auto-registration)
```

---

## 8. CAMBIOS EN BLAZOR UI (FASE 5 - UI)

### 8.1 UsuarioDialog.razor

```razor
<!-- Cambiar Email field: quitar Required, ErrorText, validación solo si tiene valor -->
<MudTextField @bind-Value="_email" 
              Label="Email" 
              Variant="Variant.Outlined" 
              Class="mb-3" 
              Autocomplete="off" 
              Required="false"
              Error="@(_submitted && !string.IsNullOrWhiteSpace(_email) && !IsValidEmail(_email))"
              ErrorText="El email no es válido" />

<!-- En CanSave(): quitar validación de Email required -->
private bool CanSave()
{
    if (IsEdit) return true;
    if (string.IsNullOrWhiteSpace(_nomUsuario)) return false;
    // QUITAR: if (string.IsNullOrWhiteSpace(_email)) return false;
    if (string.IsNullOrWhiteSpace(_nombre)) return false;
    if (string.IsNullOrWhiteSpace(_apellido)) return false;
    // ... resto
}

<!-- Agregar helper method -->
private bool IsValidEmail(string email)
{
    if (string.IsNullOrWhiteSpace(email)) return true; // null/empty es válido (opcional)
    try { var addr = new System.Net.Mail.MailAddress(email); return addr.Address == email; }
    catch { return false; }
}
```

### 8.2 UsuarioGeneral.razor

```razor
<!-- Email ya es editable, no tiene Required - mantener -->
<!-- Agregar indicador visual: "(opcional)" en label -->
<MudTextField @bind-Value="_editarEmail" Label="Email (opcional)" ... />
```

### 8.3 PasswordStrength.razor

```csharp
// En OnParametersSetAsync - pasar null si no hay email:
[Parameter] public string? Email { get; set; }

// En ValidateAsync:
var result = await Api.PostAsync<PasswordStrengthInfoDto>("api/usuarios/validar-password",
    new ValidarPasswordRequestDto
    {
        Password = Password,
        NomUsuario = NomUsuario,
        Email = string.IsNullOrWhiteSpace(Email) ? null : Email  // Pasar null si vacío
    });
```

---

## 9. CAMBIOS EN EMAIL SUBSYSTEM (FASE 6 - EMAIL)

### 9.1 EmailQueue / EmailJob

```csharp
// EmailJob.ToEmail puede ser null/empty
// En EmailBackgroundService.ProcessQueueAsync - AGREGAR validación:
await foreach (var job in _queue.ReadAllAsync(ct))
{
    if (string.IsNullOrWhiteSpace(job.ToEmail))
    {
        _logger.LogInformation("EmailJob {Kind} para usuario {IdUsuario} omitido: sin email configurado", 
            job.Kind, job.IdUsuario);
        continue; // Saltar procesamiento
    }
    // ... procesar normal
}
```

### 9.2 PassPlatEmailService - Resolución de Email

```csharp
// En SendFromJobAsync - YA resuelve email desde usuario si job.ToEmail vacío
// MEJORAR: Validar que el usuario resuelto TENGA email
if (string.IsNullOrWhiteSpace(resolvedEmail))
{
    _logger.LogInformation("No se puede enviar {Kind}: Usuario {IdUsuario} sin email", job.Kind, job.IdUsuario);
    return Result<EmailResult>.Failure("NO_EMAIL", "Usuario sin email configurado");
}
```

---

## 10. CAMBIOS EN MFA (FASE 8 - MFA)

### 10.1 Registro de MFA tipo Email

```csharp
// En MFAService o controlador de MFA - AGREGAR validación:
public async Task<Result> RegistrarMfaAsync(RegistrarMfaDto dto, CancellationToken ct)
{
    if (dto.IdTipoMFA == (int)ETipoMFA.Email)
    {
        var usuario = await _usuarioRepo.ObtenerPorIdAsync(dto.IdUsuario, ct);
        if (usuario.IsFailure || usuario.Value == null || string.IsNullOrWhiteSpace(usuario.Value.Email))
            return Result.Failure("EMAIL_REQUIRED", "Para MFA por Email, el usuario debe tener email configurado");
    }
    // ... resto
}
```

### 10.2 Login con MFA Email

```csharp
// En AuthService.EnviarCodigoMfaAsync - YA valida:
if (metodoMfa.IdTipoMFA != (int)ETipoMFA.Email) return;
// Y luego:
if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email)) return;
// CORRECTO - No envía si no hay email
```

---

## 11. PASSWORD EXPIRATION (FASE 13 - PASSWORDEXPIRATION)

### 11.1 PasswordExpirationBackgroundService

```csharp
// Cambio en filtro de usuarios:
.Where(u => !u.Eliminado && u.EmailVerificado && !string.IsNullOrWhiteSpace(u.Email))
// Solo notificar a usuarios CON email verificado y NO vacío
```

---

## 12. RIESGOS IDENTIFICADOS (FASE 12 - RIESGOS)

| Categoría | Riesgo | Mitigación |
|-----------|--------|------------|
| **Técnico** | Migración BD rompe datos existentes (emails duplicados NULL) | Índice único FILTRADO `WHERE Email IS NOT NULL` permite múltiples NULL |
| **Técnico** | SP_Auth_Login falla si ambos null | Validar en API que al menos uno venga (ya existe) |
| **Funcional** | Usuario sin Email no puede recuperar password | Flujo alternativo: admin reset / token temporal en UI |
| **Funcional** | MFA Email no disponible para usuarios sin Email | Validar al registrar MFA Email; ofrecer TOTP/SMS/WebAuthn |
| **Seguridad** | EmailVerificado=true pero Email=NULL | Validar: si EmailVerificado=true => Email NOT NULL (constraint o trigger) |
| **UX** | Usuario crea cuenta sin Email, luego no recibe notificaciones | UI: advertir "Sin email no recibirá alertas de seguridad ni recuperación" |
| **Operacional** | PasswordExpiration no notifica a usuarios sin Email | Correcto - solo notificar si hay email |
| **Compatibilidad** | EmailLog espera Destinatario NOT NULL | No crear EmailLog si no hay email (ya validado en servicio) |
| **Migración** | Datos existentes con emails duplicados (antes UNIQUE global) | El nuevo índice filtrado por Tenant + Email IS NOT NULL resuelve |

---

## 13. PLAN DE MIGRACIÓN POR FASES (FASE 13 - PLAN)

### FASE 1: Modelo de Datos (BD + EF Core) ✓ CRÍTICO
- [ ] Script SQL: ALTER TABLE Usuarios (Email NULL, índice filtrado)
- [ ] Actualizar SP_Usuario_Crear para aceptar Email NULL
- [ ] Verificar SP_Auth_Login (ya compatible)
- [ ] Actualizar UsuarioConfiguration.cs (IsRequired(false), índice filtrado)
- [ ] Generar migración EF Core
- [ ] **Validar**: Build + Tests unitarios repositorio

### FASE 2: Dominio (Entidades + DTOs + Validators) ✓ CRÍTICO
- [ ] Usuario.Crear(): Email string?
- [ ] CrearUsuarioDto.Email: string?
- [ ] UsuarioDto.Email: string?
- [ ] CrearUsuarioValidator: quitar NotEmpty, EmailAddress().When()
- [ ] **Validar**: Build + Tests unitarios validators

### FASE 3: Servicios (Lógica de Negocio) ✓ CRÍTICO
- [ ] PasswordExpirationBackgroundService: filtro Email
- [ ] PassPlatEmailService: validación temprana + log informativo
- [ ] PasswordService: log informativo
- [ ] MfaService: validar Email para MFA tipo Email
- [ ] UsuarioService: sin cambios críticos (ya usa factory)
- [ ] **Validar**: Build + Tests unitarios servicios

### FASE 4: API (Controladores) ✓
- [ ] AuthController.OlvidoPassword: flujo alternativo sin Email
- [ ] UsuariosController: sin cambios (validator automático)
- [ ] **Validar**: Build + Tests de integración API

### FASE 5: UI Blazor ✓
- [ ] UsuarioDialog: Email opcional, validación formato only
- [ ] UsuarioGeneral: label "(opcional)"
- [ ] PasswordStrength: pasar Email null
- [ ] **Validar**: Build + Playwright tests

### FASE 6: Email Subsystem ✓
- [ ] EmailBackgroundService: skip jobs sin ToEmail
- [ ] PassPlatEmailService: no crear EmailLog sin email
- [ ] **Validar**: Build + Tests email

### FASE 7: MFA ✓
- [ ] Validar registro MFA Email requiere Email usuario
- [ ] **Validar**: Tests MFA

### FASE 8: Playwright + Pruebas E2E ✓
- [ ] Crear usuario SIN Email
- [ ] Login con NomUsuario (sin Email)
- [ ] Cambio password
- [ ] Bloqueo/Desbloqueo
- [ ] Roles/Permisos
- [ ] Verificar NO errores en consola
- [ ] Verificar logs informativos "Usuario sin Email configurado"

---

## 14. COMPATIBILIDAD HACIA ATRÁS (FASE 11 - COMPATIBILIDAD)

| Módulo | Con Email | Sin Email |
|--------|-----------|-----------|
| EmailTemplates | ✅ Funciona igual | ⚠️ No se renderizan/envían |
| EmailLog | ✅ Se crea | ❌ No se crea (log informativo) |
| PasswordExpiration | ✅ Notifica | ❌ No notifica (solo log auditoría) |
| SecurityAlert | ✅ Notifica | ❌ No notifica (solo log auditoría) |
| NewDevice | ✅ Notifica | ❌ No notifica |
| NewIp | ✅ Notifica | ❌ No notifica |
| Welcome | ✅ Notifica | ❌ No notifica |
| MFA Email | ✅ Funciona | ❌ No permitido registrar |
| MFA TOTP/SMS | ✅ Funciona | ✅ Funciona igual |
| Recuperación Password | ✅ Email token | ⚠️ Admin reset / token UI |
| Auditoría | ✅ Completa | ✅ Completa (sin email logs) |

---

## 15. SCRIPTS SQL COMPLETOS

```sql
-- ============================================================
-- MIGRACIÓN COMPLETA: Usuarios sin Email
-- Ejecutar EN ORDEN
-- ============================================================

-- 1. Backup de datos (precaución)
-- SELECT * INTO Usuarios_Backup_2024 FROM dbo.Usuarios;

-- 2. Eliminar índice único global actual
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Usuarios_Email' AND object_id = OBJECT_ID('dbo.Usuarios'))
    DROP INDEX UQ_Usuarios_Email ON dbo.Usuarios;

-- 3. Hacer Email NULLable
ALTER TABLE dbo.Usuarios
ALTER COLUMN Email nvarchar(255) NULL;

-- 4. Crear índice único filtrado por Tenant + Email (solo no nulos)
CREATE UNIQUE INDEX UX_Usuarios_TenantEmail
ON dbo.Usuarios (IdTenant, Email)
WHERE (Eliminado = 0 AND Email IS NOT NULL);

-- 5. Agregar constraint: Si EmailVerificado=1 => Email NOT NULL
ALTER TABLE dbo.Usuarios
ADD CONSTRAINT CK_Usuarios_EmailVerificado_RequiereEmail
CHECK (EmailVerificado = 0 OR Email IS NOT NULL);

-- 6. Verificar/Actualizar SP_Usuario_Crear (ejemplo - ajustar según SP real)
-- El SP debe aceptar @Email nvarchar(255) = NULL
-- Y no fallar en validación UNIQUE si Email IS NULL

-- 7. Verificar trigger TR_Usuarios_Mod - ya compatible con NULL

-- 8. Actualizar estadísticas
UPDATE STATISTICS dbo.Usuarios;
```

---

## 16. PLAN DE ROLLBACK

```sql
-- ROLLBACK: Restaurar Email NOT NULL + UNIQUE global
-- SOLO si migración falla críticamente

-- 1. Restaurar datos desde backup
-- DELETE FROM dbo.Usuarios;
-- INSERT INTO dbo.Usuarios SELECT * FROM Usuarios_Backup_2024;

-- 2. O: Revertir schema
ALTER TABLE dbo.Usuarios
ALTER COLUMN Email nvarchar(255) NOT NULL;

DROP INDEX IF EXISTS UX_Usuarios_TenantEmail ON dbo.Usuarios;

CREATE UNIQUE INDEX UQ_Usuarios_Email ON dbo.Usuarios (Email);

ALTER TABLE dbo.Usuarios
DROP CONSTRAINT IF EXISTS CK_Usuarios_EmailVerificado_RequiereEmail;
```

---

## 17. CASOS DE PRUEBA (FASE 17 - CASOS DE PRUEBA)

| ID | Escenario | Resultado Esperado |
|----|-----------|-------------------|
| TC-01 | Crear usuario con Email válido | ✅ Éxito, welcome email enviado |
| TC-02 | Crear usuario SIN Email (null) | ✅ Éxito, NO welcome email, log "sin email" |
| TC-03 | Crear usuario con Email vacío "" | ✅ Tratarse como NULL, sin email |
| TC-04 | Crear usuario Email duplicado (mismo tenant) | ❌ Error UNIQUE (solo si ambos no null) |
| TC-05 | Login con NomUsuario (usuario sin Email) | ✅ Éxito, token generado |
| TC-06 | Login con Email (usuario con Email) | ✅ Éxito, token generado |
| TC-07 | Olvido password usuario CON Email | ✅ Token enviado por email |
| TC-08 | Olvido password usuario SIN Email | ⚠️ Respuesta: "Contacte administrador" |
| TC-09 | Registrar MFA Email usuario CON Email | ✅ Éxito, código enviado |
| TC-10 | Registrar MFA Email usuario SIN Email | ❌ Error: "Email requerido para MFA Email" |
| TC-11 | Registrar MFA TOTP usuario SIN Email | ✅ Éxito |
| TC-12 | PasswordExpiration usuario CON Email | ✅ Email enviado en días 15,7,3,1,0 |
| TC-13 | PasswordExpiration usuario SIN Email | ❌ No email, solo auditoría |
| TC-14 | SecurityAlert usuario SIN Email | ❌ No email, solo auditoría |
| TC-15 | Editar usuario: quitar Email (poner null) | ✅ Éxito, índice permite múltiples NULL |
| TC-16 | Editar usuario: agregar Email válido | ✅ Éxito, validación formato |
| TC-17 | Editar usuario: Email duplicado en tenant | ❌ Error UNIQUE filtrado |
| TC-18 | Bloqueo usuario SIN Email | ✅ Bloqueo OK, NO email alerta |
| TC-19 | Desbloqueo usuario SIN Email | ✅ Desbloqueo OK, NO email notificación |
| TC-20 | Cambio password admin usuario SIN Email | ✅ Cambio OK, NO email notificación |

---

## 18. SCORE DE IMPACTO

| Área | Archivos Afectados | Complejidad | Riesgo |
|------|-------------------|-------------|--------|
| Base de Datos | 3 (Tabla, Índice, Constraint) + SPs | ALTA | ALTO |
| Dominio (Entidades/DTOs) | 4 | MEDIA | MEDIO |
| Validadores | 1 | BAJA | BAJO |
| Servicios | 6 | MEDIA | MEDIO |
| API/Controllers | 2 | MEDIA | MEDIO |
| UI Blazor | 3 | MEDIA | BAJO |
| Email Subsystem | 2 | BAJA | BAJO |
| MFA | 1 | MEDIA | MEDIO |
| **TOTAL** | **~22 archivos** | **MEDIA-ALTA** | **MEDIO** |

**Esfuerzo estimado**: 8-12 horas de desarrollo + 4 horas testing

---

## 19. RECOMENDACIONES FINALES

### Arquitectónicas
1. **Separación Identidad/Canal**: Implementar `CommunicationChannel` entity futura (Email, Phone, Push) vinculada a Usuario
2. **EmailVerificado constraint**: Mantener `CHECK (EmailVerificado = 0 OR Email IS NOT NULL)` para integridad
3. **Índice filtrado**: `UX_Usuarios_TenantEmail` es crítico para performance y integridad multi-tenant

### Seguridad
1. **Recuperación sin Email**: Implementar flujo "Admin Reset" con token temporal en UI (no por email)
2. **MFA Email**: Bloquear registro si no hay Email; forzar TOTP/WebAuthn
3. **Auditoría**: Loguear siempre "Usuario sin Email configurado" para trazabilidad

### UX
1. **UI**: Label "Email (opcional)" + helper text "Sin email no recibirá alertas ni recuperación"
2. **Validación**: Solo validar formato si usuario ingresa algo
3. **Onboarding**: Mostrar beneficios de agregar email (recuperación, alertas, MFA)

### Operacional
1. **Migración**: Ejecutar en ventana de mantenimiento; backup previo obligatorio
2. **Monitoreo**: Alertar si % usuarios sin Email > umbral (ej. 20%)
3. **Documentación**: Actualizar guías de administrador y usuario final

---

## 20. PRÓXIMOS PASOS INMEDIATOS

1. **Aprobar este plan** con stakeholders
2. **Crear branch** `feature/fase13-usuarios-sin-email`
3. **Ejecutar FASE 1** (BD + EF Core) - Punto de no retorno
4. **Validar build** completo tras cada fase
5. **Ejecutar Playwright** suite completa en FASE 8

---

*Documento generado: 2026-06-27*
*Versión: 1.0*
*Autor: Arquitecto PassPlat*