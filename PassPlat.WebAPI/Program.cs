using CBP.Caching.DependencyInjection;
using CBP.Caching.Memory.DependencyInjection;
using CBP.Data.Asynchronous;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using CBP.Logging.DependencyInjection;
using CBP.MultiTenant.Abstractions;
using CBP.MultiTenant.Context;
using CBP.MultiTenant.Registration;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.WebAPI.MultiTenant;
using CBP.Security.Cryptography.Factories;
using CBP.WebApi.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PassPlat.WebAPI.Filters;
using PassPlat.WebAPI.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PassPlat.Aplicacion;
using PassPlat.Aplicacion.OAuth;
using PassPlat.Aplicacion.Options;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos;
using CBP.Data.Utilities;
using PassPlat.WebAPI.Auth;
using PassPlat.WebAPI.Middleware;
using PassPlat.WebAPI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCbpLogging(builder.Configuration);

// Route all ILogger<T> through Serilog so file sink captures middleware logs too
builder.Logging.AddSerilog();

// Register Serilog.ILogger from the static Log.Logger (needed by CBP.Logging services)
builder.Services.AddSingleton(Log.Logger);

// ExceptionHandlingMiddleware is registered as Transient by AddCbpWebApi but depends on
// RequestDelegate, which is normally provided by the middleware pipeline, not DI.
// Register a no-op delegate to satisfy DI validation (UseMiddleware provides the real one).
builder.Services.AddSingleton<RequestDelegate>(_ => static ctx => Task.CompletedTask);

builder.Services.AddCbpControllers();

builder.Services.AddTransient<TenantRequeridoExceptionFilter>();
builder.Services.Configure<MvcOptions>(o => o.Filters.Add(typeof(TenantRequeridoExceptionFilter)));
// S64: errores de negocio sin tipo (500 de CBP) con su estado HTTP correcto (ClasificacionErrores).
builder.Services.Configure<MvcOptions>(o => o.Filters.Add(new ErrorCodigoHttpFilter()));
builder.Services.AddCbpWebApi();
builder.Services.AddCbpOpenApi();

builder.Services.AddCbpAuthentication(o => o.AutoChallenge = false);
builder.Services.AddJwtOperator(options =>
{
    var jwt = builder.Configuration.GetSection("Jwt");
    var secretKey = jwt["SecretKey"]!;
    if (string.IsNullOrEmpty(secretKey) || secretKey.Length < 32)
        throw new InvalidOperationException("Jwt:SecretKey must be configured (via User Secrets, env vars, or Key Vault) and be at least 32 characters.");
    if (!builder.Environment.IsDevelopment() && secretKey.StartsWith("CHANGEME"))
        throw new InvalidOperationException("JWT SecretKey must be changed from default in production.");
    options.SecretKey = secretKey;
        options.Issuer = jwt["Issuer"]!;
        options.Audience = jwt["Audience"]!;
        var expMins = int.TryParse(jwt["ExpirationMinutes"], out var exp) ? exp : 60;
        if (expMins <= 0) throw new InvalidOperationException("JWT ExpirationMinutes must be greater than 0.");
        options.ExpirationMinutes = expMins;
        var rtExpMins = int.TryParse(jwt["RefreshTokenExpirationMinutes"], out var rtExp) ? rtExp : 1440;
        if (rtExpMins <= 0) throw new InvalidOperationException("JWT RefreshTokenExpirationMinutes must be greater than 0.");
        options.RefreshTokenExpirationMinutes = rtExpMins;
        options.ClockSkew = TimeSpan.FromMinutes(2);
        // S54.15: GAP-S54.14-01 — PassPlat must accept both PassPlat and InventaNet audiences
        // Token for INVENTA (IdApp=2) is issued with audience InventaNet via AuthenticationTokenIssuer.GenerateTokenWithAudience.
        // Without ValidAudiences, ValidateAudience with single ValidAudience=PassPlat rejects Inventa tokens (INVALID_TOKEN on /api/auth/mis-tenants).
        // Minimal blast-radius fix: keep ValidateAudience=true, ValidIssuer, signature, lifetime; expand ValidAudiences to [PassPlat, InventaNet].
        var audienceInventa = jwt["AudienceInventa"] ?? "InventaNet";
        options.ConfigureTokenValidation = p =>
        {
            p.ValidAudiences = new[] { jwt["Audience"]!, audienceInventa };
            p.ValidAudience = null; // force array check, avoid single-value precedence
        };
});

builder.Services.AddAuthentication(o => o.DefaultForbidScheme = "CbpFallback")
    .AddScheme<AuthenticationSchemeOptions, CbpForbidHandler>("CbpFallback", null);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SystemOnly", policy =>
        policy.RequireClaim("is_system", "true"));
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, LoggingAuthorizationMiddlewareResultHandler>();

// S63: limitadores particionados por IP / usuario con tope global por política
// (configuración en appsettings, sección "RateLimiting"). Ver PassPlat.WebAPI/RateLimiting.
builder.Services.AddPassPlatRateLimiting(builder.Configuration);

builder.Services.AddSqlSlowQueryInterceptor();
builder.Services.AddSqlCommandErrorInterceptor();
builder.Services.AddDbContext<PassPlatDbContext>((sp, options) =>
{
    var connStr = builder.Configuration.GetConnectionString("PassPlatDb");
    if (string.IsNullOrEmpty(connStr))
        throw new InvalidOperationException("ConnectionStrings:PassPlatDb must be configured (via User Secrets, env vars, or Key Vault).");
    options.UseSqlServer(connStr);

    // D1: EnableSensitiveDataLogging solo si Development Y configuración explícita (no existe en appsettings -> desactivado)
    if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Diagnostics:EfSensitiveDataLogging"))
        options.EnableSensitiveDataLogging();

    // D2: Eliminar la salida directa a consola; EF ya usa ILoggerFactory (Serilog) con sus filtros.
    // D4: Bajar CommandError y SaveChangesFailed a Debug; el interceptor registra en Error.
    options.ConfigureWarnings(w => w
        .Log((Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandError, Microsoft.Extensions.Logging.LogLevel.Debug))
        .Log((Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.SaveChangesFailed, Microsoft.Extensions.Logging.LogLevel.Debug)));

    options.AddInterceptors(sp.GetRequiredService<CBP.Data.Utilities.Services.SqlSlowQueryInterceptor>());
    options.AddInterceptors(sp.GetRequiredService<CBP.Data.Utilities.Services.SqlCommandErrorInterceptor>());
});

builder.Services.AddUnitOfWorkAsync<PassPlatDbContext>();
builder.Services.AddSingleton<IDistributedLockService>(sp =>
{
    var connStr = sp.GetRequiredService<IConfiguration>().GetConnectionString("PassPlatDb");
    return new SqlDistributedLockService(connStr!);
});

builder.Services.AddPassPlatDatos();
builder.Services.AddMemoryCache();
builder.Services.AddCbpCache(cache =>
{
    cache.UseLocal(new CBP.Caching.Memory.MemoryCacheProvider());
});

builder.Services.AddHttpClient("OAuth.Token", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddStandardResilienceHandler();

builder.Services.AddHttpClient("OAuth.UserInfo", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddStandardResilienceHandler();

builder.Services.AddHttpClient("OAuth.Revocation", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddStandardResilienceHandler();

builder.Services.AddHttpClient();
builder.Services.AddPassPlatAplicacion();

builder.Services.Configure<MfaOptions>(
    builder.Configuration.GetSection(MfaOptions.SectionName));
builder.Services.Configure<OAuthOptions>(
    builder.Configuration.GetSection(OAuthOptions.SectionName));

// Password hashing service (Argon2id)
builder.Services.AddSingleton<CBP.Security.Cryptography.Services.IPasswordService>(
    _ => ServiceFactory.CreateDefault());

// Encryption service (AES-256)
var encryptionKey = builder.Configuration["Encryption:Key"];
if (string.IsNullOrEmpty(encryptionKey) || encryptionKey.Length < 32)
    throw new InvalidOperationException("Encryption:Key must be configured (via User Secrets, env vars, or Key Vault) and be at least 32 characters.");
builder.Services.AddSingleton<CBP.Security.Cryptography.Services.IEncryptionService>(
    _ => new CBP.Security.Cryptography.Services.EncryptionService(encryptionKey));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
            policy.WithOrigins("http://localhost:5258", "https://localhost:5258", "http://localhost:5273", "https://localhost:7275", "https://localhost:5001")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        else
            policy.WithOrigins("https://app.passplatapp.com")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
    });
});

builder.Services.AddHealthChecks();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMultiTenantScoped<JwtTenantContext>();
builder.Services.AddScoped<ITenantResolver<Tenant>, PassPlatTenantResolver>();
builder.Services.AddSingleton<ITenantMapper<Tenant>, PassPlatTenantMapper>();
builder.Services.AddScoped<ITenantInitializer, TenantInitializer<Tenant>>();

builder.Services.Configure<PasswordExpirationOptions>(builder.Configuration.GetSection(PasswordExpirationOptions.SectionName));
// BackgroundServices: instancia concreta única → IHostedService e IBackgroundJobStatus
// resuelven la MISMA instancia (identity DI verificada por test G11).
builder.Services.AddSingleton<SesionCleanupService>();
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SesionCleanupService>());
builder.Services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<SesionCleanupService>());
builder.Services.AddSingleton<PasswordExpirationBackgroundService>();
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<PasswordExpirationBackgroundService>());
builder.Services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<PasswordExpirationBackgroundService>());

var app = builder.Build();

app.UseCbpExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHsts();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<LoggingScopeMiddleware>();
app.UseCbpAuthentication();
// S63 H10: después de la autenticación, para que las políticas por usuario vean al usuario.
app.UseRateLimiter();
app.UseMiddleware<DiagnosticAuthMiddleware>();
app.UseAuthorization();
app.UseMiddleware<DiagnosticAfterAuthMiddleware>();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
