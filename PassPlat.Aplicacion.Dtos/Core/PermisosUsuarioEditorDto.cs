namespace PassPlat.Aplicacion.Dtos.Core;

// S103 — contratos del editor de permisos de un usuario por (tenant, app): api/UsuariosPermisos/{contextos,editor,aplicar}

public class RolContextoDto
{
    public int IdRol { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class ContextoPermisoDto
{
    public int IdTenant { get; set; }
    public string TenantNombre { get; set; } = string.Empty;
    public int IdApp { get; set; }
    public string AppNombre { get; set; } = string.Empty;
    public string AppCodigo { get; set; } = string.Empty;
    public List<RolContextoDto> Roles { get; set; } = [];
}

public class PermisoEditorDto
{
    public int IdPermiso { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdModulo { get; set; }
    public string ModuloNombre { get; set; } = string.Empty;
    public byte Orden { get; set; }
    public bool PorRol { get; set; }
    public string? RolOrigen { get; set; }
    public byte? IdTipoAsigDirecto { get; set; }
    public DateTime? FecInicio { get; set; }
    public DateTime? FecFin { get; set; }
    public string? Motivo { get; set; }
    public bool DirectoVigente { get; set; }
    public bool Tiene { get; set; }
}

public class EditorPermisosDto
{
    public int IdTenant { get; set; }
    public string TenantNombre { get; set; } = string.Empty;
    public int IdApp { get; set; }
    public string AppNombre { get; set; } = string.Empty;
    public bool EsSistema { get; set; }
    public List<RolContextoDto> Roles { get; set; } = [];
    public List<PermisoEditorDto> Permisos { get; set; } = [];
}

public class CambioPermisoDto
{
    public int IdPermiso { get; set; }
    public bool Tiene { get; set; }
}

public class AplicarPermisosUsuarioDto
{
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdApp { get; set; }
    public List<CambioPermisoDto> Cambios { get; set; } = [];
    public string? Motivo { get; set; }
    public DateTime? FecInicio { get; set; }
    public DateTime? FecFin { get; set; }
}

public class ResultadoAplicarPermisosDto
{
    public int Concedidos { get; set; }
    public int Denegados { get; set; }
    public int Revocados { get; set; }
    public int SinCambio { get; set; }
}
