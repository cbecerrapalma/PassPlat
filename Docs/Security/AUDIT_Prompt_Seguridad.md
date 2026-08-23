# Audit Prompt: PassPlat Security & Identity Deep Review

**Versión:** 1.0  
**Fecha:** 2026-07-06  
**Objetivo:** Auditar, validar, mejorar y reparar los 8 pilares de seguridad del sistema de identidad PassPlat.

---

## Contexto del Sistema

PassPlat es una plataforma de gestión de identidades y accesos construida con:
- **Stack:** .NET 10, C#, Entity Framework Core, SQL Server, Blazor WebAssembly, MudBlazor
- **Arquitectura:** Clean Architecture + DDD (Dominio → Datos → Aplicación → WebAPI → Web Blazor)
- **Framework CBP:** Repository/UoW/ServiceAsync/Results/Events/Security
- **Pattern Result<T>:** Propagado en 4 capas (DB → Repo → Service → Controller → UI)
- **SPs de negocio:** Para operaciones multi-tabla (login, refresh, MFA, etc.)
- **Base de datos:** 29 tablas, 8 SPs, 3 triggers, computed columns

### Archivos Clave para Auditoría

| Capa | Archivos |
|------|----------|
| **Dominio** | `PassPlat.Dominio/Entities/Core/HistorialPwd.cs`, `IntentoAcceso.cs`, `Sesion.cs`, `TokenRest.cs`, `MFA.cs`, `Bloqueo.cs`, `AuditoriaPwd.cs`, `Acceso.cs` |
| **Datos** | `PassPlat.Datos/Repositories/SPro/AuthService.cs`, `PasswordService.cs`, `MFARepository.cs`, `SesionRepository.cs`, `AccesoRepository.cs`, `IntentoAccesoRepository.cs` |
| **Aplicación** | `PassPlat.Aplicacion/Services/SPro/AuthService.cs`, `PasswordService.cs`, `MfaService.cs`, `ExternalAuthService.cs` |
| **WebAPI** | `PassPlat.WebAPI/Controllers/AuthController.cs`, `PasswordController.cs`, `MfaController.cs`, `AccesosController.cs`, `SesionesController.cs` |
| **SQL** | `D:\CODIGOS\BBDD\PASSWORDS SP.sql` (SPs), `D:\CODIGOS\BBDD\PASSWORDS.sql` (schema) |

---

## ÁREAS DE AUDITORÍA

### ÁREA 1: HistorialPwd — Historial de Contraseñas

**Preguntas críticas:**
1. ¿Se registra CADA cambio de contraseña en `HistorialPwd`?
2. ¿Se valida que la nueva contraseña NO esté en las últimas N del historial (política de reutilización)?
3. ¿El campo `EsActual` se actualiza correctamente (solo 1 registro por usuario con `EsActual=1`)?
4. ¿Se registra el origen del cambio (`ETipoCambioPwd`: Voluntario, Forzado, Reset, PrimerUso, Expiracion, Comprometida)?
5. ¿La computed column `AnioMes` y `FecRetencion` funcionan correctamente?
6. ¿El SP `SP_Pwd_Cambiar` inserta en historial ANTES o DESPUÉS de actualizar la contraseña?
7. ¿Qué pasa si un usuario hace login con LDAP/SAML y luego quiere cambiar contraseña local?

**Archivos a revisar:**
- `PassPlat.Dominio/Entities/Core/HistorialPwd.cs`
- `PassPlat.Datos/Repositories/SPro/PasswordService.cs` (método `CambiarPasswordAsync`)
- `D:\CODIGOS\BBDD\PASSWORDS SP.sql` — SP `SP_Pwd_Cambiar`
- `PassPlat.Aplicacion/Validations/Core/` — validadores de contraseña

**Esperado:**
```csharp
// FluentValidation debe verificar:
RuleFor(x => x.NuevaPassword).Must((dto, nuevaPwd) => 
    !HistorialReciente.Contains(nuevaPwd))
    .WithMessage("La contraseña no puede ser igual a las últimas N contraseñas");
```

---

### ÁREA 2: PasswordReset — Recuperación de Contraseña

**Preguntas críticas:**
1. ¿El flujo completo funciona? (Solicitud → Token → Validación → Cambio)
2. ¿El token se genera con `SP_TokensRest_Generar` y se valida con `SP_TokensRest_Validar`?
3. ¿El token tiene expiración (15 min default)?
4. ¿El token se marca como utilizado después del cambio?
5. ¿Se envía email con el link de reset?
6. ¿Qué pasa si el usuario tiene MFA habilitado? ¿Se requiere MFA después del reset?
7. ¿Qué pasa si el usuario es de un tenant diferente?
8. ¿Se invalidan todas las sesiones del usuario después del reset?

**Archivos a revisar:**
- `PassPlat.Aplicacion/Services/SPro/AuthService.cs` — método `OlvidoPasswordAsync`
- `PassPlat.WebAPI/Controllers/AuthController.cs` — endpoint `POST /api/auth/forgot-password`
- `D:\CODIGOS\BBDD\PASSWORDS SP.sql` — SP `SP_TokensRest_Generar`, `SP_TokensRest_Validar`
- `PassPlat.Aplicacion/Services/Email/PassPlatEmailService.cs` — template `password-reset`

---

### ÁREA 3: Cambio de Contraseña

**Preguntas críticas:**
1. ¿El usuario puede cambiar su contraseña desde la UI?
2. ¿Se requiere la contraseña actual?
3. ¿Se validan las reglas de complejidad (política de contraseñas)?
4. ¿Se verifica que la nueva contraseña no esté en el historial?
5. ¿Se actualiza `HistorialPwd` correctamente?
6. ¿Se revocan otras sesiones después del cambio?
7. ¿Se envía email de notificación?
8. ¿Qué pasa si el cambio viene de un reset vs cambio voluntario?

**Archivos a revisar:**
- `PassPlat.WebAPI/Controllers/PasswordController.cs`
- `PassPlat.Aplicacion/Services/SPro/PasswordService.cs`
- `D:\CODIGOS\BBDD\PASSWORDS SP.sql` — SP `SP_Pwd_Cambiar`

---

### ÁREA 4: MFA — Autenticación Multi-Factor

**Preguntas críticas:**
1. ¿El flujo MFA está completo? (Registro → Validación → Revocación)
2. ¿Se usa TOTP (Google Authenticator) correctamente?
3. ¿El código MFA expira (configurable via `MfaOptions.TiempoValidezCodigoMFA`)?
4. ¿Se almacena el secreto TOTP de forma segura (cifrado)?
5. ¿Se puede registrar múltiples métodos MFA por usuario?
6. ¿El método principal se marca correctamente (`EsPrincipal=1`)?
7. ¿Qué pasa si el usuario pierde acceso a su dispositivo MFA?
8. ¿El SP `SP_MFA_Validar` valida correctamente el código?
9. ¿Se registra en auditoría cada intento MFA (exitoso/fallido)?
10. ¿Qué pasa con MFAEmail para usuarios sin email?

**Archivos a revisar:**
- `PassPlat.Aplicacion/Services/SPro/MfaService.cs`
- `PassPlat.WebAPI/Controllers/MfaController.cs`
- `PassPlat.Datos/Repositories/MFARepository.cs`
- `D:\CODIGOS\BBDD\PASSWORDS SP.sql` — SP `SP_MFA_Validar`
- `PassPlat.Aplicacion/Options/MfaOptions.cs`

---

### ÁREA 5: Accesos — Gestión de Roles y Permisos

**Preguntas críticas:**
1. ¿El endpoint `accesos/asignar` funciona correctamente? (Bug pre-existente de concurrencia)
2. ¿La revocación de roles funciona?
3. ¿Se valida que el usuario y el rol pertenecen al mismo tenant?
4. ¿Se registra en auditoría cada cambio de acceso?
5. ¿Se envía email de notificación al asignar/revocar roles?
6. ¿Qué pasa si se revoca el único rol de un usuario?
7. ¿El trigger `TR_Accesos_ValidarTenant` funciona correctamente?
8. ¿Los permisos se propagan correctamente a través de roles?

**Archivos a revisar:**
- `PassPlat.WebAPI/Controllers/AccesosController.cs`
- `PassPlat.Datos/Repositories/AccesoRepository.cs`
- `PassPlat.Aplicacion/Services/SPro/AccesoService.cs`
- `D:\CODIGOS\BBDD\PASSWORDS.sql` — Trigger `TR_Accesos_ValidarTenant`

---

### ÁREA 6: Auditoría — Logging de Seguridad

**Preguntas críticas:**
1. ¿Cada evento de seguridad se registra en `AuditoriaPwd` o `AuditoriaIdentidadExterna`?
2. ¿Los eventos cubiertos son: Login, LoginFallido, CambioPassword, ResetPassword, RevocacionSesiones, RegistroMFA, BloqueoCuenta, DesbloqueoCuenta?
3. ¿Se registra IP y User-Agent?
4. ¿Los computed columns `FecRetencion` funcionan para purge automático?
5. ¿El SP `SP_Purge_DatosAntiguos` respeta las retenciones?
6. ¿Se registra el resultado del intento (`EResultadoAcceso`)?
7. ¿Qué pasa con la auditoría de eventos de federación externa?

**Archivos a revisar:**
- `PassPlat.Dominio/Entities/Core/AuditoriaPwd.cs`
- `PassPlat.Dominio/Entities/Core/AuditoriaIdentidadExterna.cs`
- `PassPlat.Dominio/Enums/ETipoAuditoria.cs`
- `PassPlat.Dominio/Enums/EResultadoAcceso.cs`

---

### ÁREA 7: Email — Notificaciones

**Preguntas críticas:**
1. ¿El pipeline completo funciona? (Evento → EmailJob → Channel → SMTP → EmailLog)
2. ¿Los 22 templates están configurados?
3. ¿Se envían emails para: reset, bienvenida, cambio pwd, bloqueo, desbloqueo, MFA, roles, tenants?
4. ¿El background service funciona? (Polling, reintentos, batch)
5. ¿Las credenciales SMTP se leen de tablas (no appsettings)?
6. ¿Se respeta el `AdminEmail` para eventos de administración?
7. ¿Qué pasa si el SMTP falla? ¿Se reintenta?
8. ¿Los emails se envían de forma asíncrona (no bloquean el request)?

**Archivos a revisar:**
- `PassPlat.Aplicacion/Services/Email/EmailBackgroundService.cs`
- `PassPlat.Aplicacion/Services/Email/PassPlatEmailService.cs`
- `PassPlat.Aplicacion/Services/Email/EmailTemplateStoreService.cs`
- `PassPlat.Dominio/Entities/Core/EmailLog.cs`

---

### ÁREA 8: Contraseña Local Después de OAuth

**Preguntas críticas:**
1. ¿Un usuario que se registró con OAuth puede establecer contraseña local?
2. ¿Existe endpoint para esto? (No debería existir actualmente)
3. ¿Qué tablas se necesitan modificar para soportar esto?
4. ¿Cómo se maneja el caso donde el usuario tiene MFA pero no contraseña?
5. ¿Se puede desactivar la contraseña local y volver a OAuth?
6. ¿Qué pasa con el historial de contraseñas si el usuario nunca ha tenido contraseña local?

**Escenarios a validar:**
- Usuario se registra con Google → luego quiere usar contraseña local
- Usuario tiene contraseña local + OAuth vinculado
- Usuario con contraseña local desactiva OAuth
- Usuario OAuth intenta cambiar contraseña (debería fallar o crear contraseña local)

---

## MODELO DE MEJORA PROPUESTA

### Campo `OrigenRegistro` en HistorialPwd

**Problema:** No se sabe de dónde vino la contraseña actual (local, reset, LDAP, SAML).

**Solución:** Agregar campo `OrigenRegistro` a `HistorialPwd`:

```csharp
// HistorialPwd.cs — nuevo campo
public string OrigenRegistro { get; set; } = "LOCAL"; // LOCAL, LDAP, SAML, RESET, PRIMER_USO
```

**Valores posibles:**
- `LOCAL` — Cambio voluntario por el usuario
- `RESET` — Después de recuperación de contraseña
- `LDAP` — Sincronización desde LDAP/AD
- `SAML` — Establecido desde flujo SAML
- `PRIMER_USO` — Primer login con contraseña temporal

**Migración SQL:**
```sql
ALTER TABLE HistorialPwd ADD OrigenRegistro NVARCHAR(20) NOT NULL CONSTRAINT DF_HistorialPwd_Origen DEFAULT 'LOCAL';
```

---

## PROVEEDORES EXTERNOS (Solo 5)

### Proveedores Permitidos

| Proveedor | Código | Tipo | PKCE | JWKS | Scopes Default |
|-----------|--------|------|------|------|----------------|
| Google | `GOOGLE` | OIDC | ✅ | ✅ | `openid email profile` |
| GitHub | `GITHUB` | OAuth2 | ✅ | ❌ | `read:user user:email` |
| LinkedIn | `LINKEDIN` | OAuth2 | ✅ | ❌ | `r_liteprofile r_emailaddress` |
| Instagram | `INSTAGRAM` | OAuth2 | ✅ | ❌ | `user_profile email` |
| Facebook | `FACEBOOK` | OAuth2 | ✅ | ❌ | `email public_profile` |

### Proveedores a ELIMINAR

| Proveedor | Código | Razón |
|-----------|--------|-------|
| Microsoft | `MICROSOFT` | No está en la lista de permitidos |
| Apple | `APPLE` | No está en la lista de permitidos |

### Acciones Requeridas

1. **Eliminar archivos:**
   - `PassPlat.Aplicacion/Services/MicrosoftIdentityProvider.cs`
   - `PassPlat.Aplicacion/Services/AppleIdentityProvider.cs`

2. **Crear archivos:**
   - `PassPlat.Aplicacion/Services/InstagramIdentityProvider.cs`
   - `PassPlat.Aplicacion/Services/FacebookIdentityProvider.cs`

3. **Actualizar DI:**
   - `PassPlat.Aplicacion/AplicacionDependencyInjection.cs` — Reemplazar Microsoft/Apple con Instagram/Facebook

4. **Actualizar BD:**
   - `ProvIden` — Eliminar registros MICROSOFT y APPLE, crear INSTAGRAM y FACEBOOK

---

## UI LOGIN — Solo Logos con Tooltips

### Diseño Actual
```razor
<MudButton OnClick="@(() => IniciarConProveedorAsync(prov.Codigo))">
    @prov.Nombre  <!-- Texto del nombre del proveedor -->
</MudButton>
```

### Diseño Propuesto
```razor
<MudTooltip Text="@($"Continuar con {prov.Nombre}")">
    <MudIconButton OnClick="@(() => IniciarConProveedorAsync(prov.Codigo))"
                   Icon="@GetProviderIcon(prov.Codigo)"
                   Color="Color.Default"
                   Variant="Variant.Outlined"
                   Size="Size.Large" />
</MudTooltip>
```

### Iconos por Proveedor

| Proveedor | Icono MudBlazor | Color |
|-----------|-----------------|-------|
| Google | `@Icons.Custom.Brands.Google` | `Color.Error` |
| GitHub | `@Icons.Custom.Brands.GitHub` | `Color.Dark` |
| LinkedIn | `@Icons.Custom.Brands.LinkedIn` | `Color.Info` |
| Instagram | `@Icons.Material.Filled.CameraAlt` | `Color.Secondary` |
| Facebook | `@Icons.Custom.Brands.Facebook` | `Color.Primary` |

### Layout Propuesto

```
┌─────────────────────────────┐
│         PassPlat            │
│                             │
│  [Usuario]                  │
│  [Contraseña]               │
│  [Iniciar Sesión]           │
│                             │
│  ─── o continúa con ───     │
│                             │
│  [G] [GH] [LI] [IG] [FB]  │  ← Botones circulares con iconos
│                             │
│  ¿Olvidaste tu contraseña?  │
└─────────────────────────────┘
```

---

## CHECKLIST DE AUDITORÍA

### Antes de empezar
- [ ] Verificar que `dotnet build PassPlat.slnx` compila sin errores
- [ ] Verificar que la BD está corriendo y accesible
- [ ] Verificar que los endpoints de la API responden

### Para cada área
- [ ] Leer el código fuente completo del área
- [ ] Identificar gaps entre implementación y documentación
- [ ] Ejecutar tests existentes (si los hay)
- [ ] Probar manualmente el flujo completo
- [ ] Documentar hallazgos
- [ ] Implementar correcciones
- [ ] Ejecutar `dotnet build` después de cada cambio
- [ ] Ejecutar tests después de cada cambio

### Al finalizar
- [ ] Todos los flujos de seguridad funcionan correctamente
- [ ] No hay errores de build
- [ ] No hay warnings nuevos
- [ ] Documentación actualizada
- [ ] Migraciones SQL ejecutadas
- [ ] Proveedores actualizados (solo 5)
- [ ] UI actualizada (solo logos)

---

## COMANDOS DE VERIFICACIÓN

```bash
# Build
cd D:\CODIGOS\PassPlat
dotnet build PassPlat.slnx

# Tests FASE 12
cd tests && npx playwright test fase12-federacion-ui.spec.ts --reporter=list

# Tests FASE 13
npx playwright test fase13-usuario-sin-email.spec.ts --reporter=list

# Tests FASE 14
npx playwright test fase14-federacion-identidades.spec.ts --reporter=list

# Migraciones SQL
sqlcmd -S . -d PassPlat -U sa -P "inicio123" -i Migrations\FASE15_LdapSaml_ModelPrep.sql
```

---

## ENTREGABLES ESPERADOS

1. **Informe de Auditoría** — Hallazgos por cada área con severidad (Crítico/Alto/Medio/Bajo)
2. **Correcciones Implementadas** — Código corregido con build limpio
3. **Proveedores Actualizados** — Solo Google, GitHub, LinkedIn, Instagram, Facebook
4. **UI Actualizada** — Login con solo iconos y tooltips
5. **Modelo Mejorado** — Campo `OrigenRegistro` en HistorialPwd
6. **Migraciones SQL** — Scripts para nuevos campos/tablas
7. **Documentación** — FASE14_Documentacion_Final.md actualizado

---

## NOTAS IMPORTANTES

- **No romper funcionalidad existente** — Las correcciones deben ser backward-compatible
- **Respetar el patrón Result<T>`** — Todos los errores deben propagarse correctamente
- **SPs para operaciones multi-tabla** — No crear SPs para operaciones simples
- **EF Core para CRUD** — No usar SPs para operaciones CRUD simples
- **Build limpio** — Cada cambio debe compilar sin errores
- **Tests** — Ejecutar tests existentes después de cada cambio
