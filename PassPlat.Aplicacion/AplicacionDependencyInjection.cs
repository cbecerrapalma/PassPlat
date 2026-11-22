using System.Reflection;
using CBP.Events.DependencyInjection;
using CBP.Outbox;
using CBP.Services.Async;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Aplicacion.OAuth;
using PassPlat.Aplicacion.Options;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Services.Authentication;
using PassPlat.Aplicacion.Services.Authentication.Claims;
using PassPlat.Aplicacion.Services.Infrastructure;
using PassPlat.Aplicacion.Services.OAuth;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion;

public static class AplicacionDependencyInjection
{
    public static IServiceCollection AddPassPlatAplicacion(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddProfile<AplicacionProfile>(), typeof(AplicacionProfile));

        services.AddValidatorsFromAssembly(typeof(AplicacionDependencyInjection).Assembly);

        // Fase 1: Authentication Token Service (unified JWT generation)
        services.AddScoped<IPermissionClaimBuilder, PermissionClaimBuilder>();
        services.AddScoped<AuthenticationTokenIssuer>();
        services.AddScoped<SessionManager>();
        services.AddScoped<IAuthenticationTokenService, AuthenticationTokenService>();

        services.AddServiceAsync<IAuthService, AuthService>();
        services.AddServiceAsync<IPasswordService, PasswordService>();
        services.AddScoped<IPasswordNuevaService, PasswordNuevaService>();
        services.AddServiceAsync<ISesionService, SesionService>();
        services.AddServiceAsync<ITokenRestService, TokenRestService>();
        services.AddServiceAsync<IMFAService, MFAService>();
        services.AddServiceAsync<IBloqueoService, BloqueoService>();
        services.AddScoped<IVisibilidadUsuarioService, VisibilidadUsuarioService>();
        services.AddServiceAsync<IUsuarioService, UsuarioService>();
        services.AddServiceAsync<IAccesoService, AccesoService>();
        services.AddServiceAsync<IPoliticaPwdService, PoliticaPwdService>();
        services.AddServiceAsync<IHistorialPwdService, HistorialPwdService>();
        services.AddServiceAsync<IAuditoriaPwdService, AuditoriaPwdService>();
        services.AddServiceAsync<INotificacionService, NotificacionService>();
        services.AddServiceAsync<IDispConfiableService, DispConfiableService>();
        services.AddServiceAsync<IIntentoAccesoService, IntentoAccesoService>();
        services.AddServiceAsync<IMaintenanceService, MaintenanceService>();

        // Catalogos CRUD
        services.AddServiceAsync<ITenantService, TenantService>();
        services.AddServiceAsync<IAppService, AppService>();
        services.AddServiceAsync<IRolService, RolService>();
        services.AddServiceAsync<IConfigTenantService, ConfigTenantService>();
        services.AddServiceAsync<IConfigAppService, ConfigAppService>();
        services.AddServiceAsync<IDominioTenantService, DominioTenantService>();
        services.AddServiceAsync<IEstadoUsrService, EstadoUsrService>();
        services.AddServiceAsync<IResultadoAccesoService, ResultadoAccesoService>();
        services.AddServiceAsync<ITipoMFAService, TipoMFAService>();
        services.AddServiceAsync<IEstadoMFAService, EstadoMFAService>();
        services.AddServiceAsync<ITipoDispService, TipoDispService>();
        services.AddServiceAsync<ITipoCambioPwdService, TipoCambioPwdService>();
        services.AddServiceAsync<ITipoBloqueoService, TipoBloqueoService>();
        services.AddServiceAsync<ITipoAuditoriaService, TipoAuditoriaService>();

        // Contexto
        services.AddServiceAsync<IIPService, IPService>();

        // Core extra
        services.AddServiceAsync<IRolPoliticaPwdService, RolPoliticaPwdService>();
        services.AddServiceAsync<IRolPermisoService, RolPermisoService>();
        // S113 · D3 — «no puedes dar lo que no tienes»
        services.AddScoped<IConcesionPermisosService, ConcesionPermisosService>();

        // Permisos
        services.AddServiceAsync<IPermisoService, PermisoService>();

        // Modulos
        services.AddServiceAsync<IModuloService, ModuloService>();
        services.AddServiceAsync<IAppModuloService, AppModuloService>();
        services.AddScoped<IInyeccionCatalogoService, InyeccionCatalogoService>();
        services.AddServiceAsync<ITipoModuloService, TipoModuloService>();

        // Nuevas tablas
        services.AddServiceAsync<ITipAsigPermisoService, TipAsigPermisoService>();
        services.AddServiceAsync<IRolesHerenciaService, RolesHerenciaService>();
        services.AddServiceAsync<IGrupoService, GrupoService>();
        services.AddServiceAsync<IUsuarioPermisoService, UsuarioPermisoService>();
        services.AddServiceAsync<IGrupoUsuarioService, GrupoUsuarioService>();
        services.AddServiceAsync<IEmailLogService, EmailLogService>();

        // Password security
        services.AddSingleton<IPassPlatPasswordSecurity, PassPlatPasswordSecurity>();

        // Email
        services.AddSingleton<IEmailTemplateStoreService, EmailTemplateStoreService>();
        // EmailQueue: instancia concreta única → misma instancia p. IEmailQueue e IBackgroundJobStatus
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddScoped<IEmailAccountResolverService, EmailAccountResolverService>();
        services.AddScoped<IPassPlatEmailService, PassPlatEmailService>();
        services.AddServiceAsync<IEmailTemplateService, EmailTemplateService>();
        services.AddServiceAsync<IEmailProviderService, EmailProviderService>();
        services.AddServiceAsync<IEmailAccountService, EmailAccountService>();
        services.AddServiceAsync<ITenantEmailAccountService, TenantEmailAccountService>();
        services.AddServiceAsync<IAppEmailAccountService, AppEmailAccountService>();

        // S78: Email API para apps externas
        services.AddScoped<IEmailApiService, EmailApiService>();
        // BackgroundServices: instancia concreta única → IHostedService e IBackgroundJobStatus
        // resuelven la MISMA instancia (identity DI verificada por test G11).
        services.AddSingleton<EmailBackgroundService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<EmailBackgroundService>());
        services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<EmailBackgroundService>());
        services.AddSingleton<IdenExtTokensRotacionJob>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<IdenExtTokensRotacionJob>());
        services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<IdenExtTokensRotacionJob>());

        // S21 / S136 · D8: Outbox genérico de CBP (store = OutboxRepository de PassPlat.Datos, tabla dbo.Outbox)
        services.AddCbpOutbox<OutboxRepository>();
        services.AddCbpOutboxEvento<NewIpDetectedEvent>();
        services.AddCbpOutboxEvento<NewDeviceDetectedEvent>();
        services.AddScoped<IOutboxPublicacionFiltro, NuevaIpOutboxFiltro>();
        services.AddCbpOutboxProcesador<PassPlat.Aplicacion.Services.Infrastructure.OutboxProcessor>();
        services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<PassPlat.Aplicacion.Services.Infrastructure.OutboxProcessor>());

        // MFA temp code store
        services.AddSingleton<IMfaCodeStore, MfaCodeStore>();

        // S16.2: CBP.Events — pipeline de eventos (dispatcher + publisher DI)
        services.AddCBPEvents();
        services.AddEventHandlersFromAssembly(typeof(AplicacionDependencyInjection).Assembly);

        // FASE 17.1+17.6: OAuth state + anti-replay via IDistributedCache (Memory en dev, Redis en prod)
        // Registrado en Program.cs via AddDistributedMemoryCache() / AddStackExchangeRedisCache()

        // FASE 14 / S129: Federación solo OAuth2 (Google, Microsoft, GitHub, Instagram, Facebook)
        services.AddScoped<IExternalIdentityProvider, GoogleIdentityProvider>();
        services.AddScoped<IExternalIdentityProvider, MicrosoftIdentityProvider>();
        services.AddScoped<IExternalIdentityProvider, GitHubIdentityProvider>();
        services.AddScoped<IExternalIdentityProvider, InstagramIdentityProvider>();
        services.AddScoped<IExternalIdentityProvider, FacebookIdentityProvider>();
        services.AddServiceAsync<IExternalAuthService, ExternalAuthService>();
        services.AddServiceAsync<IProvIdenService, ProvIdenService>();
        services.AddServiceAsync<IConfProvIdenService, ConfProvIdenService>();
        services.AddServiceAsync<IIdenExtervice, IdenExtervice>();
        services.AddServiceAsync<IEstIdenExtService, EstIdenExtService>();
        services.AddServiceAsync<IHistorialIdenExtService, HistorialIdenExtService>();

        // FASE 10: Dashboard federación

        // FASE 17: Dashboard Enterprise
        services.AddScoped<IDashboardEnterpriseService, DashboardEnterpriseService>();
        services.AddScoped<IBackgroundStatusService, BackgroundStatusService>();

        // S77: inicio por perfil (GET api/inicio)
        services.AddScoped<IInicioService, InicioService>();

        // FASE 17.3.3: OAuth Catalog Validation

        // External login provider listing
        services.AddScoped<IExternalProviderValidator, ExternalProviderValidator>();
        services.AddScoped<IExternalLoginProviderService, ExternalLoginProviderService>();

        // S125 — retorno del login externo con código de canje
        services.AddScoped<ILoginExternoCanjeService, LoginExternoCanjeService>();

        return services;
    }
}
