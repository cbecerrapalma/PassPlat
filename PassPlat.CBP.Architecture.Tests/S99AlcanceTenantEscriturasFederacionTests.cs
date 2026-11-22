using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S99 · P57 — las escrituras y lecturas por id/usuario de federación pasan el alcance de tenant
/// de la sesión al servicio, y el alta valida el tenant del cuerpo.
/// </summary>
public sealed class S99AlcanceTenantEscriturasFederacionTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Leer(string controlador) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.WebAPI", "Controllers", controlador));

    [Theory]
    [InlineData("IdenExtController.cs", 8)]
    [InlineData("ConfProvIdenController.cs", 3)] // S122 · D1: + Activar
    public void S99_controlador_pasa_el_alcance_al_servicio_y_valida_el_alta(string archivo, int llamadasConAlcance)
    {
        var fuente = Leer(archivo);

        Assert.Equal(llamadasConAlcance, Regex.Matches(fuente, @"_service\.\w+\([^;]*, AlcanceTenant, ct\)").Count);
        Assert.Contains("if (FueraDeAlcance(dto.IdTenant)) return NotFound();", fuente);
    }
}
