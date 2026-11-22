using System.Text.RegularExpressions;

namespace PassPlat.Aplicacion.Tests.Tests.S60;

// S60 · D6 — el rol global INVITADO_STORE (cuenta invitada de la tienda) debe llevar SOLO
// el permiso EMAIL_API_ENVIAR (minimo privilegio) y las exenciones por rol deben apuntar
// a la politica SIN_VENCIMIENTO. El contrato se fija sobre las fuentes de verdad de la
// migracion S60 y sobre las semillas (seeds) de la base de datos.
public class S60InvitadoStorePermisosTests
{
    private static string? BuscarRaizBBDD()
    {
        var candidatas = new[]
        {
            @"D:\CODIGOS\BBDD\PassPlat",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "..", "..", "CODIGOS", "BBDD", "PassPlat")
        };
        return candidatas.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && Directory.Exists(c));
    }

    private static string Leer(string raiz, params string[] relativas)
    {
        var ruta = Path.Combine(new[] { raiz }.Concat(relativas).ToArray());
        Assert.True(File.Exists(ruta), "No se encuentra el fichero " + ruta);
        return File.ReadAllText(ruta);
    }

    [Fact]
    public void La_semilla_asigna_al_rol_INVITADO_STORE_solo_EMAIL_API_ENVIAR()
    {
        var raiz = BuscarRaizBBDD();
        if (raiz is null)
        {
            Assert.Skip("No se encuentra la carpeta BBDD\\PassPlat en este equipo.");
            return;
        }

        var seed = Leer(raiz, "Seed", "Configuracion", "03_RolesGlobales.sql");

        // Bloque de permisos del rol invitado: un unico codigo de permiso.
        var bloque = Regex.Matches(seed, @"MERGE dbo\.RolesPermisos[\s\S]*?;")
            .Select(m => m.Value)
            .FirstOrDefault(b => b.Contains("INVITADO_STORE", StringComparison.OrdinalIgnoreCase)
                              || b.Contains("@IdRolInvitado", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(bloque);
        var permisos = Regex.Matches(bloque!, @"N'([A-Z_]+)'")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
        Assert.Equal(new[] { "EMAIL_API_ENVIAR" }, permisos);
    }

    [Fact]
    public void La_migracion_S60_crea_la_politica_SIN_VENCIMIENTO_el_rol_y_las_dos_exenciones()
    {
        var raiz = BuscarRaizBBDD();
        if (raiz is null)
        {
            Assert.Skip("No se encuentra la carpeta BBDD\\PassPlat en este equipo.");
            return;
        }

        var migracion = Leer(raiz, "Migrations", "S60", "03_Politica_Rol_Invitado.sql");

        // Politica SIN_VENCIMIENTO: global (IdTenant e IdApp a NULL), 0 dias y SoloPorRol = 1.
        Assert.Matches(@"\(\s*2,\s*NULL,\s*NULL,\s*1,\s*N'SIN_VENCIMIENTO'", migracion);
        Assert.Matches(@"1,\s*1,\s*1,\s*1,\s*1,\s*1,\s*1,\s*0,\s*24,\s*5,\s*30,\s*1,\s*1", migracion);

        // Rol global INVITADO_STORE.
        Assert.Matches(@"\(\s*18,\s*NULL,\s*N'INVITADO_STORE'", migracion);

        // Su unico permiso es el de envio de correo por API.
        Assert.Contains("EMAIL_API_ENVIAR", migracion);
        Assert.Contains("@IdPermisoEnvio int = (SELECT Id FROM dbo.Permisos WHERE Codigo = N'EMAIL_API_ENVIAR')", migracion);

        // Exenciones: (PLATFORM, PLATFORM_ADMIN) y (VESTUARIO, INVITADO_STORE).
        Assert.Contains("(1, @IdRolPlatformAdmin, @IdPoliticaSinVencimiento)", migracion);
        Assert.Contains("(3, @IdRolInvitado, @IdPoliticaSinVencimiento)", migracion);
    }
}
