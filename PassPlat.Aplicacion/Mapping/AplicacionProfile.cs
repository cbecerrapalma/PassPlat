using AutoMapper;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Contexto;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Contexto;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Mapping;

public class AplicacionProfile : Profile
{
    public AplicacionProfile()
    {
        // Catalogos
        CreateMap<Tenant, TenantDto>();
        CreateMap<CrearTenantDto, Tenant>();

        CreateMap<ConfigTenant, ConfigTenantDto>()
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null));

        CreateMap<App, AppDto>();
        CreateMap<CrearAppDto, App>();

        CreateMap<Rol, RolDto>()
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null));
        CreateMap<CrearRolDto, Rol>();

        // Contexto
        CreateMap<Disp, DispDto>()
            .ForMember(d => d.TipoDispNombre, o => o.MapFrom(s => s.TipoDisp != null ? s.TipoDisp.Nombre : null));

        CreateMap<IP, IPDto>();

        CreateMap<UserAgent, UserAgentDto>();

        // Core - Usuario
        CreateMap<Usuario, UsuarioDto>()
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null))
            .ForMember(d => d.EstadoNombre, o => o.MapFrom(s => s.Estado != null ? s.Estado.Nombre : null));
        CreateMap<CrearUsuarioDto, Usuario>();
        CreateMap<ActualizarUsuarioDto, Usuario>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        // Core - Acceso
        CreateMap<Acceso, AccesoDto>()
            .ForMember(d => d.AppNombre, o => o.MapFrom(s => s.App != null ? s.App.Nombre : null))
            .ForMember(d => d.RolNombre, o => o.MapFrom(s => s.Rol != null ? s.Rol.Nombre : null))
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null))
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null));
        CreateMap<AsignarAccesoDto, Acceso>();

        // Core - PoliticaPwd
        CreateMap<PoliticaPwd, PoliticaPwdDto>()
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null))
            .ForMember(d => d.AppNombre, o => o.MapFrom(s => s.App != null ? s.App.Nombre : null));
        CreateMap<CrearPoliticaPwdDto, PoliticaPwd>();
        CreateMap<ActualizarPoliticaPwdDto, PoliticaPwd>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        // Core - HistorialPwd
        CreateMap<HistorialPwd, HistorialPwdDto>()
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null))
            .ForMember(d => d.PoliticaNombre, o => o.MapFrom(s => s.Politica != null ? s.Politica.Nombre : null));

        // Core - Sesion
        CreateMap<Sesion, SesionDto>()
            .ForMember(d => d.AppNombre, o => o.MapFrom(s => s.App != null ? s.App.Nombre : null))
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null))
            .ForMember(d => d.IPDireccion, o => o.MapFrom(s => s.DireccionIP != null ? s.DireccionIP.Direccion : null))
            .ForMember(d => d.DispModelo, o => o.MapFrom(s => s.Disp != null ? $"{s.Disp.Fabricante} {s.Disp.Modelo}".Trim() : null));
        CreateMap<CrearSesionDto, Sesion>();

        // Core - TokenRest
        CreateMap<TokenRest, TokenRestDto>()
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null))
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null))
            .ForMember(d => d.AppNombre, o => o.MapFrom(s => s.App != null ? s.App.Nombre : null));
        CreateMap<GenerarTokenRestDto, TokenRest>();

        // Core - IntentoAcceso
        CreateMap<IntentoAcceso, IntentoAccesoDto>()
            .ForMember(d => d.ResultadoNombre, o => o.MapFrom(s => s.Resultado != null ? s.Resultado.Nombre : null))
            .ForMember(d => d.IPDireccion, o => o.MapFrom(s => s.DireccionIP != null ? s.DireccionIP.Direccion : null));
        CreateMap<RegistrarIntentoAccesoDto, IntentoAcceso>();

        // Core - Bloqueo
        CreateMap<Bloqueo, BloqueoDto>()
            .ForMember(d => d.TipoBloqueoNombre, o => o.MapFrom(s => s.TipoBloqueo != null ? s.TipoBloqueo.Nombre : null));
        CreateMap<CrearBloqueoDto, Bloqueo>();

        // Core - MFA
        CreateMap<MFA, MFADto>()
            .ForMember(d => d.TipoMFANombre, o => o.MapFrom(s => s.TipoMFA != null ? s.TipoMFA.Nombre : null))
            .ForMember(d => d.EstadoMFANombre, o => o.MapFrom(s => s.Estado != null ? s.Estado.Nombre : null));
        CreateMap<RegistrarMFADto, MFA>();

        // Core - AuditoriaPwd
        CreateMap<AuditoriaPwd, AuditoriaPwdDto>()
            .ForMember(d => d.TipoAccionNombre, o => o.MapFrom(s => s.TipoAccion != null ? s.TipoAccion.Nombre : null))
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null));
        CreateMap<RegistrarAuditoriaPwdDto, AuditoriaPwd>();

        // Core - Notificacion
        CreateMap<Notificacion, NotificacionDto>();
        CreateMap<CrearNotificacionDto, Notificacion>();

        // Core - RolPoliticaPwd
        CreateMap<RolPoliticaPwd, RolPoliticaPwdDto>()
            .ForMember(d => d.RolNombre, o => o.MapFrom(s => s.Rol != null ? s.Rol.Nombre : null))
            .ForMember(d => d.PoliticaNombre, o => o.MapFrom(s => s.Politica != null ? s.Politica.Nombre : null));
        CreateMap<CrearRolPoliticaPwdDto, RolPoliticaPwd>();

        // Core - DispConfiable
        CreateMap<DispConfiable, DispConfiableDto>()
            .ForMember(d => d.DispModelo, o => o.MapFrom(s => s.Disp != null ? s.Disp.Modelo : null))
            .ForMember(d => d.DispFabricante, o => o.MapFrom(s => s.Disp != null ? s.Disp.Fabricante : null))
            .ForMember(d => d.DispTipo, o => o.MapFrom(s => s.Disp != null && s.Disp.TipoDisp != null ? s.Disp.TipoDisp.Nombre : null))
            .ForMember(d => d.IP, o => o.MapFrom(s => s.Disp != null ? s.Disp.IP : null))
            .ForMember(d => d.Pais, o => o.MapFrom(s => s.Disp != null ? s.Disp.Pais : null))
            .ForMember(d => d.Navegador, o => o.MapFrom(s => s.Disp != null ? s.Disp.Navegador : null))
            .ForMember(d => d.SO, o => o.MapFrom(s => s.Disp != null ? s.Disp.SO : null))
            .ForMember(d => d.ProveedorAuth, o => o.MapFrom(s => s.Disp != null ? s.Disp.ProveedorAuth : null))
            .ForMember(d => d.CantidadLogins, o => o.MapFrom(s => s.Disp != null ? s.Disp.CantidadLogins : 0))
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null));
        CreateMap<CrearDispConfiableDto, DispConfiable>();

        // Catalogos - ConfigApp
        CreateMap<ConfigApp, ConfigAppDto>()
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null));
        CreateMap<CrearConfigAppDto, ConfigApp>();
        CreateMap<ActualizarConfigAppDto, ConfigApp>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        // Catalogos - DominioTenant
        CreateMap<DominioTenant, DominioTenantDto>()
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null));
        CreateMap<CrearDominioTenantDto, DominioTenant>();

        // Catalogos - EstadoUsr
        CreateMap<EstadoUsr, EstadoUsrDto>();

        // Catalogos - EstIdenExt
        CreateMap<EstIdenExt, EstIdenExtDto>();

        // Catalogos - ResultadoAcceso
        CreateMap<ResultadoAcceso, ResultadoAccesoDto>();

        // Catalogos - TipoMFA
        CreateMap<TipoMFA, TipoMFADto>();

        // Catalogos - EstadoMFA
        CreateMap<EstadoMFA, EstadoMFADto>();

        // Catalogos - TipoDisp
        CreateMap<TipoDisp, TipoDispDto>();

        // Catalogos - TipoCambioPwd
        CreateMap<TipoCambioPwd, TipoCambioPwdDto>();

        // Catalogos - TipoBloqueo
        CreateMap<TipoBloqueo, TipoBloqueoDto>();

        // Catalogos - TipoAuditoria
        CreateMap<TipoAuditoria, TipoAuditoriaDto>();

        // Modulos
        CreateMap<Modulo, ModuloDto>()
            .ForMember(d => d.ModuloPadreNombre, o => o.MapFrom(s => s.ModuloPadre != null ? s.ModuloPadre.Nombre : null))
            .ForMember(d => d.TipoModuloCodigo, o => o.MapFrom(s => s.TipoModulo != null ? s.TipoModulo.Codigo : null));
        CreateMap<CrearModuloDto, Modulo>();
        CreateMap<ActualizarModuloDto, Modulo>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        // AppsModulos
        CreateMap<AppModulo, AppModuloDto>()
            .ForMember(d => d.AppNombre, o => o.MapFrom(s => s.App != null ? s.App.Nombre : null))
            .ForMember(d => d.ModuloNombre, o => o.MapFrom(s => s.Modulo != null ? s.Modulo.Nombre : null))
            .ForMember(d => d.ModuloCodigo, o => o.MapFrom(s => s.Modulo != null ? s.Modulo.Codigo : null));
        CreateMap<CrearAppModuloDto, AppModulo>();

        // Permisos
        CreateMap<Permiso, PermisoDto>()
            .ForMember(d => d.ModuloNombre, o => o.MapFrom(s => s.Modulo != null ? s.Modulo.Nombre : null))
            .ForMember(d => d.ModuloCodigo, o => o.MapFrom(s => s.Modulo != null ? s.Modulo.Codigo : null));
        CreateMap<CrearPermisoDto, Permiso>();

        // Core - EmailTemplate
        CreateMap<EmailTemplate, EmailTemplateDto>()
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null));
        CreateMap<CrearEmailTemplateDto, EmailTemplate>();
        CreateMap<ActualizarEmailTemplateDto, EmailTemplate>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        // Core - EmailTemplatePartial
        CreateMap<EmailTemplatePartial, EmailTemplatePartialDto>();
        CreateMap<CrearEmailTemplatePartialDto, EmailTemplatePartial>();
        CreateMap<ActualizarEmailTemplatePartialDto, EmailTemplatePartial>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        // Core - EmailTemplateHistorial
        CreateMap<EmailTemplateHistorial, EmailTemplateHistorialDto>()
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null));

        // Core - RolPermiso
        CreateMap<RolPermiso, RolPermisoDto>()
            .ForMember(d => d.PermisoCodigo, o => o.MapFrom(s => s.Permiso != null ? s.Permiso.Codigo : null))
            .ForMember(d => d.PermisoNombre, o => o.MapFrom(s => s.Permiso != null ? s.Permiso.Nombre : null))
            .ForMember(d => d.Modulo, o => o.MapFrom(s => s.Permiso != null && s.Permiso.Modulo != null ? s.Permiso.Modulo.Codigo : null));
        CreateMap<AsignarPermisoDto, RolPermiso>();

        // Catalogos - TipAsigPermiso
        CreateMap<TipAsigPermiso, TipAsigPermisoDto>();

        // Catalogos - RolesHerencia
        CreateMap<RolesHerencia, RolesHerenciaDto>()
            .ForMember(d => d.RolHijoNombre, o => o.MapFrom(s => s.RolHijo != null ? s.RolHijo.Nombre : null))
            .ForMember(d => d.RolPadreNombre, o => o.MapFrom(s => s.RolPadre != null ? s.RolPadre.Nombre : null))
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null));
        CreateMap<CrearRolesHerenciaDto, RolesHerencia>();

        // Catalogos - Grupo
        CreateMap<Grupo, GrupoDto>()
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null));
        CreateMap<CrearGrupoDto, Grupo>();
        CreateMap<ActualizarGrupoDto, Grupo>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        // Core - UsuarioPermiso
        CreateMap<UsuarioPermiso, UsuarioPermisoDto>()
            .ForMember(d => d.PermisoCodigo, o => o.MapFrom(s => s.Permiso != null ? s.Permiso.Codigo : null))
            .ForMember(d => d.PermisoNombre, o => o.MapFrom(s => s.Permiso != null ? s.Permiso.Nombre : null))
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null));
        CreateMap<CrearUsuarioPermisoDto, UsuarioPermiso>();

        // Core - GrupoUsuario
        CreateMap<GrupoUsuario, GrupoUsuarioDto>()
            .ForMember(d => d.GrupoNombre, o => o.MapFrom(s => s.Grupo != null ? s.Grupo.Nombre : null))
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null));
        CreateMap<CrearGrupoUsuarioDto, GrupoUsuario>();

        // Core - EmailLog
        CreateMap<EmailLog, EmailLogDto>();

        // Catalogos - EmailProvider
        CreateMap<EmailProvider, EmailProviderDto>();

        // Catalogos - TipoModulo
        CreateMap<TipoModulo, TipoModuloDto>();

        // Core - EmailAccount
        CreateMap<EmailAccount, EmailAccountDto>()
            .ForMember(d => d.ProviderNombre, o => o.MapFrom(s => s.Provider != null ? s.Provider.Nombre : null));
        CreateMap<CrearEmailAccountDto, EmailAccount>();
        CreateMap<ActualizarEmailAccountDto, EmailAccount>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        // Core - TenantEmailAccount
        CreateMap<TenantEmailAccount, TenantEmailAccountDto>()
            .ForMember(d => d.EmailAccountNombre, o => o.MapFrom(s => s.EmailAccount != null ? s.EmailAccount.Nombre : null))
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null));
        CreateMap<CrearTenantEmailAccountDto, TenantEmailAccount>();

        // Core - AppEmailAccount
        CreateMap<AppEmailAccount, AppEmailAccountDto>()
            .ForMember(d => d.EmailAccountNombre, o => o.MapFrom(s => s.EmailAccount != null ? s.EmailAccount.Nombre : null))
            .ForMember(d => d.AppNombre, o => o.MapFrom(s => s.App != null ? s.App.Nombre : null));
        CreateMap<CrearAppEmailAccountDto, AppEmailAccount>();

        // Catalogos - ProvIden
        CreateMap<ProvIden, ProvIdenDto>()
            .ForMember(d => d.Metadata, o => o.MapFrom(s => s.Metadata != null
                ? System.Text.Json.JsonSerializer.Serialize(s.Metadata, (System.Text.Json.JsonSerializerOptions?)null)
                : null));
        CreateMap<CrearProvIdenDto, ProvIden>();
        CreateMap<ActualizarProvIdenDto, ProvIden>()
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<ProvIden, ProviderConfigurationInfoDto>()
            .ForMember(d => d.AuthorizationEndpoint, o => o.MapFrom(s => s.EndpointAutorizacion))
            .ForMember(d => d.TokenEndpoint, o => o.MapFrom(s => s.EndpointToken))
            .ForMember(d => d.JwksUri, o => o.MapFrom(s => s.JwksUri))
            .ForMember(d => d.Issuer, o => o.MapFrom(s => s.UrlIssuer))
            .ForMember(d => d.SoportaPKCE, o => o.MapFrom(s => s.SoportaPKCE))
            .ForMember(d => d.SoportaRefreshToken, o => o.MapFrom(s => s.SoportaRefreshToken))
            .ForMember(d => d.SoportaMFA, o => o.MapFrom(s => s.SoportaMFA));

        // Catalogos - ConfProvIden
        CreateMap<ConfProvIden, ConfProvIdenDto>()
            .ForMember(d => d.ProvIdenNombre, o => o.MapFrom(s => s.ProvIden != null ? s.ProvIden.Nombre : null))
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null))
            .ForMember(d => d.RolDefectoNombre, o => o.MapFrom(s => s.RolDefectoNav != null ? s.RolDefectoNav.Nombre : null))
            .ForMember(d => d.TieneClientSecret, o => o.MapFrom(s => !string.IsNullOrWhiteSpace(s.ClientSecret)))
            .ForMember(d => d.FechaCambioSecret, o => o.MapFrom(s => s.FecMod));
        CreateMap<CrearConfProvIdenDto, ConfProvIden>();
        CreateMap<ActualizarConfProvIdenDto, ConfProvIden>()
            .ForMember(d => d.ClientSecret, o => o.Ignore())
            .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<Rol, RolLookupDto>();

        // Core - IdenExt
        CreateMap<IdenExt, IdenExtDto>()
            .ForMember(d => d.ProvIdenNombre, o => o.MapFrom(s => s.ProvIden != null ? s.ProvIden.Nombre : null))
            .ForMember(d => d.ProvIdenCodigo, o => o.MapFrom(s => s.ProvIden != null ? s.ProvIden.Codigo : null))
            .ForMember(d => d.ProvIdenIcono, o => o.MapFrom(s => s.ProvIden != null ? s.ProvIden.Icono : null))
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null))
            .ForMember(d => d.EstadoNombre, o => o.MapFrom(s => s.Estado != null ? s.Estado.Nombre : null))
            .ForMember(d => d.EstadoColor, o => o.MapFrom(s => s.Estado != null ? s.Estado.Color : null))
            .ForMember(d => d.DispositivoModelo, o => o.MapFrom(s => s.Dispositivo != null ? s.Dispositivo.Modelo : null))
            .ForMember(d => d.UltimoTenantNombre, o => o.MapFrom(s => s.UltimoTenantNav != null ? s.UltimoTenantNav.Nombre : null))
            .ForMember(d => d.UsuarioRevocaNombre, o => o.MapFrom(s => s.UsuarioRevoca != null ? s.UsuarioRevoca.NomUsuario : null));
        CreateMap<CrearIdenExtDto, IdenExt>();

        // Core - HistorialIdenExt
        CreateMap<HistorialIdenExt, HistorialIdenExtDto>()
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null))
            .ForMember(d => d.ProvIdenNombre, o => o.MapFrom(s => s.ProvIden != null ? s.ProvIden.Nombre : null))
            .ForMember(d => d.RealizadoPorNombre, o => o.MapFrom(s => s.RealizadoPorNav != null ? s.RealizadoPorNav.NomUsuario : null));

        // Core - AudIdenExt
        CreateMap<AudIdenExt, AudIdenExtDto>()
            .ForMember(d => d.TenantNombre, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Nombre : null))
            .ForMember(d => d.ProvIdenNombre, o => o.MapFrom(s => s.ProvIden != null ? s.ProvIden.Nombre : null))
            .ForMember(d => d.UsuarioNombre, o => o.MapFrom(s => s.Usuario != null ? s.Usuario.NomUsuario : null));
        CreateMap<AudIdenExt, AudIdenExtResumenDto>()
            .ForMember(d => d.ProvIdenNombre, o => o.MapFrom(s => s.ProvIden != null ? s.ProvIden.Nombre : null));
    }
}
