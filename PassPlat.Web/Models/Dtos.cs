using System.Text.Json.Serialization;

namespace PassPlat.Web.Models;

// ─── Paged Response ────────────────────────────────
public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}

// ─── Dispositivos (create only - no Aplicacion counterpart) ──
public class CrearDispDto
{
    public int IdTipoDisp { get; set; }
    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }
}

// ─── Password ────────────────────────────────────────
public class CambiarPasswordDto
{
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public string PasswordActual { get; set; } = "";
    public string HashPwdNuevo { get; set; } = "";
    public byte PepperVersion { get; set; } = 1;
    public int IdTipoCambio { get; set; } = 1;
    public int? IdDisp { get; set; }
    public int? IdIP { get; set; }
    public int? IdAgente { get; set; }
}

public class ValidarPasswordDto
{
    public string Password { get; set; } = "";
    public int? IdTenant { get; set; }
    public int? IdApp { get; set; }
}

// ─── Auth — Result types ─────────────────────────────
public class LoginResult
{
    public bool Success { get; set; }
    public bool RequiereMFA { get; set; }
    public bool ReqCambioPwd { get; set; }
    public int? IdMFAPrincipal { get; set; }
    public int? IdTipoMFA { get; set; }
    public int? IdUsuario { get; set; }
    public int? IdTenant { get; set; }
    public string? NomUsuario { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ValidarMfaRequest
{
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdTipoMFA { get; set; }
    public string IdMFA { get; set; } = "";
}



// ─── App ──────────────────────────────────────
public class AppItem
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
}

// ─── Tenant Resolution ─────────────────────────────
public class TenantInfoResult
{
    public int? IdTenant { get; set; }
    public string? NombreTenant { get; set; }
    public string? CodigoTenant { get; set; }
    public bool RequiereSeleccion { get; set; }
}

public class TenantItem
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
}

// ─── Mantenimiento ───────────────────────────────────
public class PurgeRequest
{
    public int DiasRetencion { get; set; } = 365;
}
