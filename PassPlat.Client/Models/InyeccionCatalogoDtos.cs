namespace PassPlat.Client.Models;

// S99 D6 — Espejo, dentro del SDK, del contrato POST api/apps/{codigoApp}/catalogo de PassPlat
// (PassPlat.Aplicacion.Dtos.Catalogos.InyeccionCatalogoDto). El SDK no referencia los DTO del servidor.

internal sealed class CatalogoSolicitud
{
    public bool Simular { get; set; }
    public string? HuellaEsperada { get; set; }
    public List<string> DesactivarSobrantes { get; set; } = [];
    public string CodigoTipoModulo { get; set; } = "SHARED";
    public List<ModuloCatalogoSolicitud> Modulos { get; set; } = [];
    public List<PermisoCatalogoSolicitud> Permisos { get; set; } = [];
}

internal sealed class ModuloCatalogoSolicitud
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Ruta { get; set; }
    public string? Icono { get; set; }
    public int Orden { get; set; }
    public string? CodigoPadre { get; set; }
    public bool EsVisibleMenu { get; set; }
    public List<string> Acciones { get; set; } = [];
}

internal sealed class PermisoCatalogoSolicitud
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string CodigoModulo { get; set; } = string.Empty;
}

internal sealed class ResultadoCatalogoRespuesta
{
    public string CodigoApp { get; set; } = string.Empty;
    public bool Simulado { get; set; }
    public string HuellaRemota { get; set; } = string.Empty;
    public List<OperacionCatalogoRespuesta> Operaciones { get; set; } = [];
    public List<string> Sobrantes { get; set; } = [];
}

internal sealed class OperacionCatalogoRespuesta
{
    public string Tipo { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? CodigoModulo { get; set; }
    public string? Detalle { get; set; }
}

/// <summary>ProblemDetails de PassPlat: <c>errorCode</c> y, en CATALOGO_INVALIDO/COLISION, la lista <c>errors</c>.</summary>
internal sealed class ProblemaRespuesta
{
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public string? ErrorCode { get; set; }
    public Dictionary<string, string>? Errors { get; set; }
}
