using System.Text.Json;

namespace PassPlat.Web.Tests.Infra;

/// <summary>Datos de prueba (mismo catalogo real que el servidor: 12 apps, 16 roles, 4 tenants).</summary>
internal static class Datos
{
    public static readonly (int Id, string Codigo, string Nombre)[] Apps =
    {
        (1, "PASSPLAT", "AccessPlat"),
        (3, "CACHE_1586564928", "x"),
        (4, "CACHE_142037557", "x"),
        (5, "CACHE_1365409577", "x"),
        (6, "GC_MSJVGPPP", "GateC App msjvgppp"),
        (7, "GC_MSJW80OB", "GateC App msjw80ob"),
        (8, "GC_MSJWPY0D", "GateC App msjwpy0d"),
        (9, "GC_MSJWXYYD", "GateC App msjwxyyd"),
        (10, "GC_MSJXQM8S", "GateC App msjxqm8s"),
        (11, "GC_MSJZOA4B", "GateC App msjzoa4b"),
        (12, "INVENTA", "Inventa POS"),
        (13, "INVENTA_STORE", "Inventa Store")
    };

    public static readonly (int Id, int? IdTenant, string Codigo, string Nombre, bool Activo)[] Roles =
    {
        (1, null, "PLATFORM_ADMIN", "Administrador", true),
        (2, null, "PLATFORM_EDITOR", "Editor", true),
        (3, null, "PLATFORM_SUPERVISOR", "Supervisor", true),
        (4, null, "PLATFORM_CONSULTA", "Consulta", true),
        (5, 2, "ABARROTES_ADMIN", "Administrador", true),
        (6, 2, "ABARROTES_EDITOR", "Editor", true),
        (7, 2, "ABARROTES_SUPERVISOR", "Supervisor", true),
        (8, 2, "ABARROTES_CONSULTA", "Consulta", true),
        (9, 3, "VESTUARIO_ADMIN", "Administrador", true),
        (10, 3, "VESTUARIO_EDITOR", "Editor", true),
        (11, 3, "VESTUARIO_SUPERVISOR", "Supervisor", true),
        (12, 3, "VESTUARIO_CONSULTA", "Consulta", true),
        (13, 4, "E2E_ADMIN", "Administrador", true),
        (14, 4, "E2E_EDITOR", "Editor", true),
        (15, 4, "E2E_SUPERVISOR", "Supervisor", true),
        (16, 4, "E2E_CONSULTA", "Consulta", true)
    };

    public static string AppsJson(int cantidad = 12)
    {
        var lista = Apps.Take(cantidad).Select(a => new
        {
            id = a.Id,
            codigo = a.Codigo,
            nombre = a.Nombre,
            urlBase = (string?)null,
            activa = true,
            fecCrea = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }).ToList();
        return JsonSerializer.Serialize(lista);
    }

    public static string RolesJson()
    {
        var lista = Roles.Select(r => new
        {
            id = r.Id,
            idTenant = r.IdTenant,
            codigo = r.Codigo,
            nombre = r.Nombre,
            descripcion = (string?)null,
            activo = r.Activo,
            fecCrea = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            tenantNombre = (string?)null
        }).ToList();
        return JsonSerializer.Serialize(lista);
    }

    public static string TenantsJson()
    {
        var lista = new[]
        {
            new { id = 1, codigo = "PLATFORM", nombre = "Plataforma", activo = true, esSistema = true, fecCrea = DateTime.UnixEpoch },
            new { id = 2, codigo = "ABARROTES", nombre = "Abarrotes del Sur", activo = true, esSistema = false, fecCrea = DateTime.UnixEpoch },
            new { id = 3, codigo = "VESTUARIO", nombre = "Vestuario SA de CV", activo = true, esSistema = false, fecCrea = DateTime.UnixEpoch },
            new { id = 4, codigo = "E2E_TEST", nombre = "Inventa E2E Test", activo = true, esSistema = false, fecCrea = DateTime.UnixEpoch }
        }.ToList();
        return JsonSerializer.Serialize(lista);
    }

    public static string UsuariosJson() => JsonSerializer.Serialize(new[]
    {
        new { id = 1, idTenant = 1, idEstado = 1, nomUsuario = "sistema", email = "sistema@passplat.app", emailVerificado = true, nombre = "Usuario", apellido = "Sistema", reqCambioPwd = false, intentosFallidos = 0, esSistema = true, tienePasswordLocal = true, eliminado = false },
        new { id = 2, idTenant = 1, idEstado = 1, nomUsuario = "admin_plataforma", email = "admin@passplat.app", emailVerificado = true, nombre = "Admin", apellido = "Plataforma", reqCambioPwd = false, intentosFallidos = 0, esSistema = false, tienePasswordLocal = true, eliminado = false }
    });

    public static string AccesosJson(params (int IdUsuario, int IdApp, int IdRol, string RolNombre)[] accesos)
    {
        var lista = accesos.Select(a => new
        {
            id = a.IdRol,
            idUsuario = a.IdUsuario,
            idTenant = 1,
            idApp = a.IdApp,
            idRol = a.IdRol,
            activo = true,
            fecAsignacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            appNombre = "Inventa POS",
            rolNombre = a.RolNombre,
            usuarioNombre = "sistema",
            tenantNombre = "Plataforma"
        }).ToList();
        return JsonSerializer.Serialize(lista);
    }
}
