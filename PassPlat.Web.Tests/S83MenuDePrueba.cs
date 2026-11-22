using System.Text.Json;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Web.Tests;

/// <summary>S83 — menús de prueba con la forma de <c>GET api/modulos/menu</c>.</summary>
internal static class S83MenuDePrueba
{
    /// <summary>Las 33 rutas de los módulos activos de PASSPLAT tras la migración S83 (D2).</summary>
    public static readonly string[] RutasDelSeed =
    [
        "/", "/accesos", "/admin/grupos", "/admin/matriz-permisos", "/admin/panel", "/admin/permisos",
        "/admin/roles", "/admin/roles-permisos", "/apps", "/auditoria", "/bloqueos", "/config-app",
        "/config-tenants", "/disp-confiables", "/dispositivos", "/dominios-tenant", "/email/accounts",
        "/email/app-accounts", "/email/providers", "/email/tenant-accounts", "/email-templates",
        "/federacion/confproviden", "/federacion/iden-ext", "/federacion/providen", "/historial-pwd",
        "/admin/herencia-roles", "/admin/modulos", "/admin/apps-modulos", "/email/historial",
        "/intentos-acceso", "/mantenimiento", "/mfa", "/notificaciones", "/politicas-pwd", "/sesiones",
        "/tenants", "/usuarios"
    ];

    public static ModuloMenuDto Nodo(string codigo, string nombre, string? ruta, string? icono = null,
        bool menu = true, string? descripcion = null, params ModuloMenuDto[] hijos) => new()
    {
        Codigo = codigo,
        Nombre = nombre,
        Ruta = ruta,
        Icono = icono,
        Descripcion = descripcion,
        EsVisibleMenu = menu,
        Hijos = [.. hijos]
    };

    /// <summary>Árbol mínimo de PASSPLAT: enlace suelto, grupos y módulos solo para el buscador.</summary>
    public static List<ModuloMenuDto> Arbol() =>
    [
        Nodo("PASSPLAT.INICIO", "Panel principal", "/", "Dashboard", descripcion: "Resumen de la cuenta y accesos rápidos"),
        Nodo("IAM", "IAM", null, "AdminPanelSettings", menu: false, hijos:
        [
            Nodo("IAM.USUARIOS", "Usuarios", "/usuarios", "People", descripcion: "Gestión completa de usuarios", hijos:
                [Nodo("IAM.PANEL", "Panel de administración", "/admin/panel", "SpaceDashboard", descripcion: "Resumen del sistema IAM")]),
            Nodo("IAM.ROLES", "Roles", "/admin/roles", "Badge", descripcion: "Administración de roles del sistema"),
            Nodo("IAM.SESIONES", "Sesiones", "/sesiones", "DevicesOther", menu: false, descripcion: "Sesiones activas de usuarios")
        ]),
        Nodo("AUDITORIA", "Auditoría", null, "History", menu: false, hijos:
            [Nodo("AUDITORIA.EVENTOS", "Eventos de auditoría", "/auditoria", "History", descripcion: "Eventos de seguridad del sistema")]),
        Nodo("EMAIL", "Correos", null, "Email", menu: false, hijos:
            [Nodo("EMAIL.PROVIDERS", "Proveedores de correo", "/email/providers", "Dns", descripcion: "Proveedores de envío de correo")]),
        Nodo("INFRAESTRUCTURA", "Infraestructura", null, "Dns", menu: false, hijos:
        [
            Nodo("INFRAESTRUCTURA.DISPCONFIABLES", "Dispositivos confiables", "/disp-confiables", "Devices", menu: false, descripcion: "Dispositivos autorizados")
        ])
    ];

    public static string Json(List<ModuloMenuDto>? arbol = null) =>
        JsonSerializer.Serialize(arbol ?? Arbol(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
