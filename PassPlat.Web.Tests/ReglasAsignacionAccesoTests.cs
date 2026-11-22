using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Web.Pages.Accesos;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S54 · K1 — reglas puras de asignacion de accesos (D3).
/// S54.1 · K2 (D5) — el aviso de reemplazo describe el rol y el tenant del acceso existente.
/// </summary>
public class ReglasAsignacionAccesoTests
{
    private static List<RolDto> Roles() => Datos.Roles.Select(r => new RolDto
    {
        Id = r.Id,
        IdTenant = r.IdTenant,
        Codigo = r.Codigo,
        Nombre = r.Nombre,
        Activo = r.Activo,
        FecCrea = DateTime.UnixEpoch
    }).ToList();

    private static AccesoDto Existente(int idRol = 1, int idTenant = 1, string? rol = "Consultor", string? tenant = "Abarrotes del Sur") => new()
    {
        Id = 1,
        IdUsuario = 1,
        IdApp = 12,
        IdTenant = idTenant,
        IdRol = idRol,
        RolNombre = rol,
        TenantNombre = tenant
    };

    [Fact]
    public void Solo_roles_activos_globales_o_del_tenant_de_la_sesion()
    {
        var roles = Roles();
        roles.Add(new RolDto { Id = 99, IdTenant = 1, Codigo = "PLATFORM_INACTIVO", Nombre = "Inactivo", Activo = false, FecCrea = DateTime.UnixEpoch });

        var permitidos = ReglasAsignacionAcceso.RolesPermitidos(roles, idTenantSesion: 1);

        Assert.Equal(4, permitidos.Count);
        Assert.All(permitidos, r => Assert.True(r.Activo));
        Assert.All(permitidos, r => Assert.True(r.IdTenant is null || r.IdTenant == 1));
        Assert.DoesNotContain(permitidos, r => r.Codigo == "ABARROTES_ADMIN");
        Assert.DoesNotContain(permitidos, r => r.Codigo == "E2E_ADMIN");
    }

    [Fact]
    public void Con_otro_tenant_de_sesion_solo_caben_sus_roles_y_los_globales()
    {
        var permitidos = ReglasAsignacionAcceso.RolesPermitidos(Roles(), idTenantSesion: 2);

        Assert.Equal(8, permitidos.Count);
        Assert.Contains(permitidos, r => r.Codigo == "PLATFORM_ADMIN");
        Assert.Contains(permitidos, r => r.Codigo == "ABARROTES_ADMIN");
        Assert.DoesNotContain(permitidos, r => r.Codigo == "VESTUARIO_ADMIN");
    }

    [Fact]
    public void No_se_puede_asignar_si_falta_usuario_app_o_rol()
    {
        var usuario = new UsuarioDto { Id = 1, NomUsuario = "sistema" };
        var app = new AppDto { Id = 12, Codigo = "INVENTA", Nombre = "Inventa POS" };
        var rol = new RolDto { Id = 1, Codigo = "PLATFORM_ADMIN", Nombre = "Administrador" };

        Assert.True(ReglasAsignacionAcceso.PuedeAsignar(usuario, app, rol));
        Assert.False(ReglasAsignacionAcceso.PuedeAsignar(null, app, rol));
        Assert.False(ReglasAsignacionAcceso.PuedeAsignar(usuario, null, rol));
        Assert.False(ReglasAsignacionAcceso.PuedeAsignar(usuario, app, null));
        Assert.False(ReglasAsignacionAcceso.PuedeAsignar(null, null, null));
    }

    [Fact]
    public void Busca_el_acceso_existente_por_app()
    {
        var accesos = new List<AccesoDto>
        {
            new() { Id = 1, IdUsuario = 1, IdApp = 1, IdRol = 1, RolNombre = "Administrador" },
            new() { Id = 2, IdUsuario = 1, IdApp = 12, IdRol = 1, RolNombre = "Administrador" }
        };

        Assert.Equal(12, ReglasAsignacionAcceso.BuscarAccesoExistente(accesos, 12)!.IdApp);
        Assert.Null(ReglasAsignacionAcceso.BuscarAccesoExistente(accesos, 13));
    }

    [Fact]
    public void El_texto_de_reemplazo_usa_el_rol_y_el_tenant_del_acceso_existente()
    {
        var existente = Existente(idRol: 1, idTenant: 1, rol: "Consultor", tenant: "Abarrotes del Sur");
        var rolNuevo = new RolDto { Id = 2, Codigo = "PLATFORM_EDITOR", Nombre = "Editor" };

        var texto = ReglasAsignacionAcceso.TextoReemplazo(existente, rolNuevo, idTenantSesion: 1, nombreTenantSesion: "Plataforma");

        Assert.NotNull(texto);
        Assert.Contains("Reemplaza el rol Consultor (tenant Abarrotes del Sur) por Editor en el tenant de tu sesión (Plataforma)", texto);
        Assert.DoesNotContain("Reemplaza el rol Administrador", texto);
    }

    [Fact]
    public void Con_el_mismo_rol_y_tenant_no_hay_cambios_que_guardar()
    {
        var existente = Existente(idRol: 1, idTenant: 1, rol: "Administrador", tenant: "Plataforma");
        var rolNuevo = new RolDto { Id = 1, Codigo = "PLATFORM_ADMIN", Nombre = "Administrador" };

        var texto = ReglasAsignacionAcceso.TextoReemplazo(existente, rolNuevo, idTenantSesion: 1, nombreTenantSesion: "Plataforma");

        Assert.NotNull(texto);
        Assert.Contains("Ya tiene el rol Administrador en el tenant Plataforma para esta app: no hay cambios que guardar.", texto);
        Assert.True(ReglasAsignacionAcceso.EsMismoAcceso(existente, rolNuevo, idTenantSesion: 1));
    }

    [Fact]
    public void Con_otro_tenant_no_es_el_mismo_acceso()
    {
        var existente = Existente(idRol: 1, idTenant: 2, rol: "Administrador", tenant: "Abarrotes del Sur");
        var rolNuevo = new RolDto { Id = 1, Codigo = "PLATFORM_ADMIN", Nombre = "Administrador" };

        Assert.False(ReglasAsignacionAcceso.EsMismoAcceso(existente, rolNuevo, idTenantSesion: 1));
        var texto = ReglasAsignacionAcceso.TextoReemplazo(existente, rolNuevo, idTenantSesion: 1, nombreTenantSesion: "Plataforma");
        Assert.NotNull(texto);
        Assert.Contains("Reemplaza el rol Administrador (tenant Abarrotes del Sur)", texto);
    }

    [Fact]
    public void Sin_acceso_existente_no_hay_texto_de_reemplazo()
    {
        var rolNuevo = new RolDto { Id = 1, Codigo = "PLATFORM_ADMIN", Nombre = "Administrador" };

        Assert.Null(ReglasAsignacionAcceso.TextoReemplazo(null, rolNuevo, idTenantSesion: 1, nombreTenantSesion: "Plataforma"));
    }

    /// <summary>
    /// S54.2 · K1 (D1) — dos roles con el mismo nombre y distinto Id no son el mismo acceso.
    /// </summary>
    [Fact]
    public void Dos_roles_con_el_mismo_nombre_no_son_el_mismo_aceso()
    {
        var existente = Existente(idRol: 1, idTenant: 1, rol: "Administrador", tenant: "Plataforma");
        var mismoNombreOtroId = new RolDto { Id = 5, IdTenant = 2, Codigo = "ABARROTES_ADMIN", Nombre = "Administrador" };

        Assert.False(ReglasAsignacionAcceso.EsMismoAcceso(existente, mismoNombreOtroId, idTenantSesion: 1));

        var texto = ReglasAsignacionAcceso.TextoReemplazo(existente, mismoNombreOtroId, idTenantSesion: 1, nombreTenantSesion: "Plataforma");

        Assert.NotNull(texto);
        Assert.Contains("Reemplaza el rol Administrador (tenant Plataforma) por Administrador en el tenant de tu sesión (Plataforma)", texto);
        Assert.DoesNotContain("no hay cambios", texto);
    }

    /// <summary>
    /// S54.2 · K1 (D1) — el mismo IdRol en el tenant de la sesion es el mismo acceso;
    /// con otro tenant en el acceso existente, es un reemplazo.
    /// </summary>
    [Fact]
    public void El_mismo_id_de_rol_es_el_mismo_aceso_y_en_otro_tenant_no()
    {
        var existente = Existente(idRol: 1, idTenant: 1, rol: "Administrador", tenant: "Plataforma");
        var mismoRol = new RolDto { Id = 1, Codigo = "PLATFORM_ADMIN", Nombre = "Administrador" };

        Assert.True(ReglasAsignacionAcceso.EsMismoAcceso(existente, mismoRol, idTenantSesion: 1));
        Assert.Contains(
            "no hay cambios que guardar",
            ReglasAsignacionAcceso.TextoReemplazo(existente, mismoRol, idTenantSesion: 1, nombreTenantSesion: "Plataforma")!);

        var otroTenant = Existente(idRol: 1, idTenant: 2, rol: "Administrador", tenant: "Abarrotes del Sur");

        Assert.False(ReglasAsignacionAcceso.EsMismoAcceso(otroTenant, mismoRol, idTenantSesion: 1));
        Assert.Contains(
            "Reemplaza el rol Administrador (tenant Abarrotes del Sur)",
            ReglasAsignacionAcceso.TextoReemplazo(otroTenant, mismoRol, idTenantSesion: 1, nombreTenantSesion: "Plataforma")!);
    }

    /// <summary>
    /// S54.2 · K2 (D2) — sin rol elegido el aviso describe el acceso existente,
    /// pero no afirma que no haya cambios.
    /// </summary>
    [Fact]
    public void Con_rol_nulo_el_aviso_no_dice_que_no_hay_cambios()
    {
        var existente = Existente(idRol: 1, idTenant: 1, rol: "Administrador", tenant: "Plataforma");

        var texto = ReglasAsignacionAcceso.TextoReemplazo(existente, rolNuevo: null, idTenantSesion: 1, nombreTenantSesion: "Plataforma");

        Assert.NotNull(texto);
        Assert.Equal("Ya tiene el rol Administrador en el tenant Plataforma para esta app.", texto);
        Assert.DoesNotContain("no hay cambios", texto);
    }
}
