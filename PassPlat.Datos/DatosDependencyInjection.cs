using Microsoft.Extensions.DependencyInjection;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;

namespace PassPlat.Datos;

public static class DatosDependencyInjection
{
    public static IServiceCollection AddPassPlatDatos(this IServiceCollection services)
    {
        // Catalogos — concrete + interface forwarding (same instance)
        AddScopedWithInterface<TenantRepository, ITenantRepository>(services);
        AddScopedWithInterface<ConfigTenantRepository, IConfigTenantRepository>(services);
        AddScopedWithInterface<ConfigAppRepository, IConfigAppRepository>(services);
        AddScopedWithInterface<DominioTenantRepository, IDominioTenantRepository>(services);
        AddScopedWithInterface<AppRepository, IAppRepository>(services);
        AddScopedWithInterface<RolRepository, IRolRepository>(services);
        AddScopedWithInterface<EstadoUsrRepository, IEstadoUsrRepository>(services);
        AddScopedWithInterface<ResultadoAccesoRepository, IResultadoAccesoRepository>(services);
        AddScopedWithInterface<TipoMFARepository, ITipoMFARepository>(services);
        AddScopedWithInterface<EstadoMFARepository, IEstadoMFARepository>(services);
        AddScopedWithInterface<TipoDispRepository, ITipoDispRepository>(services);
        AddScopedWithInterface<TipoCambioPwdRepository, ITipoCambioPwdRepository>(services);
        AddScopedWithInterface<TipoBloqueoRepository, ITipoBloqueoRepository>(services);
        AddScopedWithInterface<TipoAuditoriaRepository, ITipoAuditoriaRepository>(services);

        // Contexto
        AddScopedWithInterface<DispRepository, IDispRepository>(services);
        AddScopedWithInterface<IPRepository, IIPRepository>(services);
        AddScopedWithInterface<UserAgentRepository, IUserAgentRepository>(services);

        // Core
        AddScopedWithInterface<UsuarioRepository, IUsuarioRepository>(services);
        AddScopedWithInterface<AccesoRepository, IAccesoRepository>(services);
        AddScopedWithInterface<PoliticaPwdRepository, IPoliticaPwdRepository>(services);
        AddScopedWithInterface<RolPoliticaPwdRepository, IRolPoliticaPwdRepository>(services);
        AddScopedWithInterface<HistorialPwdRepository, IHistorialPwdRepository>(services);
        AddScopedWithInterface<IntentoAccesoRepository, IIntentoAccesoRepository>(services);
        AddScopedWithInterface<BloqueoRepository, IBloqueoRepository>(services);
        AddScopedWithInterface<AuditoriaPwdRepository, IAuditoriaPwdRepository>(services);
        AddScopedWithInterface<DispConfiableRepository, IDispConfiableRepository>(services);
        AddScopedWithInterface<NotificacionRepository, INotificacionRepository>(services);
        AddScopedWithInterface<EmailTemplateRepository, IEmailTemplateRepository>(services);
        AddScopedWithInterface<EmailTemplatePartialRepository, IEmailTemplatePartialRepository>(services);
        AddScopedWithInterface<EmailTemplateHistorialRepository, IEmailTemplateHistorialRepository>(services);
        AddScopedWithInterface<PermisoRepository, IPermisoRepository>(services);
        AddScopedWithInterface<RolPermisoRepository, IRolPermisoRepository>(services);
        AddScopedWithInterface<ModuloRepository, IModuloRepository>(services);
        AddScopedWithInterface<AppModuloRepository, IAppModuloRepository>(services);

        // Email subsystem
        AddScopedWithInterface<EmailProviderRepository, IEmailProviderRepository>(services);
        AddScopedWithInterface<EmailAccountRepository, IEmailAccountRepository>(services);
        AddScopedWithInterface<TenantEmailAccountRepository, ITenantEmailAccountRepository>(services);
        AddScopedWithInterface<AppEmailAccountRepository, IAppEmailAccountRepository>(services);

        // Catalogos faltantes
        AddScopedWithInterface<TipoModuloRepository, ITipoModuloRepository>(services);

        // Nuevas tablas: UsuariosPermisos, RolesHerencia, Grupos, GruposUsuarios, EmailLog, TipAsigPermiso
        AddScopedWithInterface<TipAsigPermisoRepository, ITipAsigPermisoRepository>(services);
        AddScopedWithInterface<RolesHerenciaRepository, IRolesHerenciaRepository>(services);
        AddScopedWithInterface<GrupoRepository, IGrupoRepository>(services);
        AddScopedWithInterface<UsuarioPermisoRepository, IUsuarioPermisoRepository>(services);
        AddScopedWithInterface<GrupoUsuarioRepository, IGrupoUsuarioRepository>(services);
        AddScopedWithInterface<EmailLogRepository, IEmailLogRepository>(services);

        // SP Repositories (require IUnitOfWorkAsync for RawQuery)
        AddScopedWithInterface<AuthRepository, IAuthRepository>(services);
        AddScopedWithInterface<PasswordRepository, IPasswordRepository>(services);
        AddScopedWithInterface<MFARepository, IMFARepository>(services);
        AddScopedWithInterface<SesionRepository, ISesionRepository>(services);
        AddScopedWithInterface<TokenRestRepository, ITokenRestRepository>(services);
        services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();

        // FASE 14: Federación de Identidades
        AddScopedWithInterface<ProvIdenRepository, IProvIdenRepository>(services);
        AddScopedWithInterface<ConfProvIdenRepository, IConfProvIdenRepository>(services);
        AddScopedWithInterface<IdenExtRepository, IIdenExtRepository>(services);
        AddScopedWithInterface<AudIdenExtRepository, IAudIdenExtRepository>(services);
        services.AddScoped<IExternalAuthRepository, ExternalAuthRepository>();

        // FASE 16: EstIdenExt + HistorialIdenExt
        AddScopedWithInterface<EstIdenExtRepository, IEstIdenExtRepository>(services);
        AddScopedWithInterface<HistorialIdenExtRepository, IHistorialIdenExtRepository>(services);

        // FASE 17.7: IdenExtTokens (Refresh Token storage)
        AddScopedWithInterface<IdenExtTokensRepository, IIdenExtTokensRepository>(services);

        // A1.4: UsuarioTenant
        AddScopedWithInterface<UsuarioTenantRepository, IUsuarioTenantRepository>(services);

        // S21: Outbox (standalone repository, no IRepositoryAsync)
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        return services;
    }

    private static void AddScopedWithInterface<TConcrete, TInterface>(IServiceCollection services)
        where TConcrete : class, TInterface
        where TInterface : class
    {
        services.AddScoped<TConcrete>();
        services.AddScoped<TInterface>(sp => sp.GetRequiredService<TConcrete>());
    }
}
