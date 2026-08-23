# Project Map

## PassPlat.Dominio

```
PassPlat.Dominio/
├── Constantes/
│   └── Codigos.cs              # String constants for enum codes
├── Enums/                      # 8 enums (prefixed with E)
│   ├── EEstadoUsuario.cs
│   ├── EEstadoMFA.cs
│   ├── EResultadoAcceso.cs
│   ├── ETipoAuditoria.cs
│   ├── ETipoBloqueo.cs
│   ├── ETipoCambioPwd.cs
│   ├── ETipoDisp.cs
│   └── ETipoMFA.cs
├── Entities/
│   ├── Catalogos/              # 13 catalog entities
│   │   ├── Tenant.cs
│   │   ├── ConfigTenant.cs
│   │   ├── DominioTenant.cs
│   │   ├── App.cs
│   │   ├── EstadoUsr.cs
│   │   ├── Rol.cs
│   │   ├── ResultadoAcceso.cs
│   │   ├── TipoMFA.cs
│   │   ├── EstadoMFA.cs
│   │   ├── TipoDisp.cs
│   │   ├── TipoCambioPwd.cs
│   │   ├── TipoBloqueo.cs
│   │   └── TipoAuditoria.cs
│   ├── Contexto/               # 3 context entities
│   │   ├── Disp.cs
│   │   ├── DireccionIP.cs
│   │   └── UserAgent.cs
│   └── Core/                   # 13 core entities
│       ├── Usuario.cs
│       ├── Acceso.cs
│       ├── PoliticaPwd.cs
│       ├── RolPoliticaPwd.cs
│       ├── HistorialPwd.cs
│       ├── Sesion.cs
│       ├── TokenRest.cs
│       ├── IntentoAcceso.cs
│       ├── Bloqueo.cs
│       ├── MFA.cs
│       ├── AuditoriaPwd.cs
│       ├── DispConfiable.cs
│       └── Notificacion.cs
└── GlobalUsings.cs
```

## PassPlat.Datos

```
PassPlat.Datos/
├── Configurations/
│   ├── Catalogos/              # 13 EF configs
│   ├── Contexto/               # 3 EF configs
│   └── Core/                   # 13 EF configs
├── Repositories/               # Custom repos (interface + impl per file)
│   ├── AuthRepository.cs
│   ├── PasswordRepository.cs
│   ├── MFARepository.cs
│   ├── SesionRepository.cs
│   ├── TokenRestRepository.cs
│   ├── TenantRepository.cs
│   ├── AppRepository.cs
│   ├── RolRepository.cs
│   ├── ConfigTenantRepository.cs
│   ├── DominioTenantRepository.cs
│   ├── EstadoUsrRepository.cs
│   ├── ResultadoAccesoRepository.cs
│   ├── TipoMFARepository.cs
│   ├── EstadoMFARepository.cs
│   ├── TipoDispRepository.cs
│   ├── TipoCambioPwdRepository.cs
│   ├── TipoBloqueoRepository.cs
│   ├── TipoAuditoriaRepository.cs
│   ├── DispRepository.cs
│   ├── DireccionIPRepository.cs
│   ├── UserAgentRepository.cs
│   ├── UsuarioRepository.cs
│   ├── AccesoRepository.cs
│   ├── PoliticaPwdRepository.cs
│   ├── RolPoliticaPwdRepository.cs
│   ├── HistorialPwdRepository.cs
│   ├── IntentoAccesoRepository.cs
│   ├── BloqueoRepository.cs
│   ├── MfaRepository.cs (MFARepository)
│   ├── AuditoriaPwdRepository.cs
│   ├── DispConfiableRepository.cs
│   ├── NotificacionRepository.cs
│   └── MaintenanceRepository.cs
├── SPResults/                  # DTOs for SP result sets
├── PassPlatDbContext.cs
├── DatosDependencyInjection.cs
└── GlobalUsings.cs (if exists)
```

## PassPlat.Aplicacion

```
PassPlat.Aplicacion/
├── Dtos/
│   ├── Catalogos/CatalogosDto.cs
│   ├── Contexto/ContextoDto.cs
│   └── Core/                   # 13 DTO files
├── Mapping/
│   └── AplicacionProfile.cs
├── Validations/
│   ├── Catalogos/CatalogosValidators.cs
│   └── Core/CoreValidators.cs
├── Interfaces/
│   └── ICustomServices.cs      # All service interfaces
├── Services/                   # 28 service implementations
│   ├── AuthService.cs
│   ├── AccesoService.cs
│   ├── BloqueoService.cs
│   ├── SesionService.cs
│   ├── TokenRestService.cs
│   ├── UsuarioService.cs
│   ├── AllServices.cs         # HistorialPwd, AuditoriaPwd, etc.
│   └── CatalogServices.cs     # Tenant, App, Rol, etc.
└── AplicacionDependencyInjection.cs
```
