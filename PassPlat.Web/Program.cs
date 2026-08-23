using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using PassPlat.Web;
using PassPlat.Web.Models;
using PassPlat.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings { AppId = 1 };
builder.Services.AddSingleton(appSettings);

builder.Services.AddMudServices();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("USUARIOS_VER", policy => policy.RequireClaim("permiso", "USUARIOS_VER"));
});

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5001"),
    Timeout = TimeSpan.FromSeconds(30)
});

var host = builder.Build();

var authStateProvider = host.Services.GetRequiredService<AuthenticationStateProvider>() as CustomAuthenticationStateProvider;
if (authStateProvider != null)
    await authStateProvider.InitializeFromStorageAsync();

await host.RunAsync();
