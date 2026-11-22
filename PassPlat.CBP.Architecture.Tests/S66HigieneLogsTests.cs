using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PassPlat.CBP.Architecture.Tests;

public class S66HigieneLogsTests
{
    private readonly string _programPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PassPlat.WebAPI", "Program.cs");

    [Fact]
    public void Program_NoUsaLogToConsole()
    {
        var content = File.ReadAllText(_programPath);
        Assert.DoesNotContain("LogTo(Console.WriteLine", content);
    }

    [Fact]
    public void Program_SensitiveDataLogging_SoloConInterruptor()
    {
        var content = File.ReadAllText(_programPath);
        Assert.Contains("Diagnostics:EfSensitiveDataLogging", content);
        Assert.Contains("EnableSensitiveDataLogging", content);
        Assert.Contains("IsDevelopment", content);
    }

    [Fact]
    public void Program_RegistraInterceptorYBajaCommandError()
    {
        var content = File.ReadAllText(_programPath);
        Assert.Contains("SqlCommandErrorInterceptor", content);
        Assert.Contains("RelationalEventId.CommandError", content);
        Assert.Contains("CoreEventId.SaveChangesFailed", content);
        Assert.Contains("LogLevel.Debug", content);
    }

    [Fact]
    public void UsuarioService_y_Controller_NoAdjuntanExcepcion()
    {
        var usuarioServicePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PassPlat.Aplicacion", "Services", "BBDD", "UsuarioService.cs");
        var controllerPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PassPlat.WebAPI", "Controllers", "UsuariosController.cs");

        var usuarioServiceContent = File.ReadAllText(usuarioServicePath);
        var controllerContent = File.ReadAllText(controllerPath);

        // No deben contener LogWarning(ex, ...)
        Assert.DoesNotContain("LogWarning(ex,", usuarioServiceContent);
        Assert.DoesNotContain("LogWarning(ex,", controllerContent);

        // Deben usar SqlErrorRedactor
        Assert.Contains("SqlErrorRedactor.GetErrorNumber", usuarioServiceContent);
        Assert.Contains("SqlErrorRedactor.Redact", usuarioServiceContent);
        Assert.Contains("SqlErrorRedactor.GetErrorNumber", controllerContent);
        Assert.Contains("SqlErrorRedactor.Redact", controllerContent);
    }
}