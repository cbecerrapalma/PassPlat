namespace PassPlat.Aplicacion.Dtos.Catalogos;

/// <summary>
/// S99 D2 — Cuerpo de <c>POST api/apps/{codigoApp}/catalogo</c>: el catálogo de una app (módulos y permisos detectados)
/// que PassPlat inyecta en una sola transacción. Contrato genérico: cualquier app puede usarlo.
/// </summary>
public class InyeccionCatalogoDto
{
    /// <summary>true = devuelve el plan sin escribir.</summary>
    public bool Simular { get; set; }

    /// <summary>Huella del plan revisado (<see cref="ResultadoInyeccionCatalogoDto.HuellaRemota"/>). Obligatoria si no se simula.</summary>
    public string? HuellaEsperada { get; set; }

    /// <summary>Sobrantes confirmados para desactivar (subconjunto de <see cref="ResultadoInyeccionCatalogoDto.Sobrantes"/>).</summary>
    public List<string> DesactivarSobrantes { get; set; } = [];

    /// <summary>Tipo de módulo de los módulos nuevos cuando la raíz de la app aún no existe.</summary>
    public string CodigoTipoModulo { get; set; } = "SHARED";

    public List<ModuloCatalogoDto> Modulos { get; set; } = [];

    /// <summary>Permisos sueltos detectados (el VER de cada ventana y sus acciones declaradas los genera PassPlat).</summary>
    public List<PermisoCatalogoDto> Permisos { get; set; } = [];
}

/// <summary>Módulo del catálogo: la raíz (<c>{APP}</c>), un agrupador (sin <c>Ruta</c>) o una ventana (con <c>Ruta</c>).</summary>
public class ModuloCatalogoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Ruta { get; set; }
    public string? Icono { get; set; }
    public int Orden { get; set; }
    public string? CodigoPadre { get; set; }
    public bool EsVisibleMenu { get; set; } = true;

    /// <summary>
    /// Acciones de la ventana además de VER (que siempre se crea): las que su código exige, p. ej. CREAR, EDITAR o ANULAR.
    /// S103: PassPlat ya no agrega CREAR, EDITAR ni ELIMINAR por su cuenta.
    /// </summary>
    public List<string> Acciones { get; set; } = [];
}

/// <summary>Permiso suelto: se crea (o se mueve) en el módulo indicado, que debe estar en el catálogo.</summary>
public class PermisoCatalogoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string CodigoModulo { get; set; } = string.Empty;
}

/// <summary>Respuesta: el plan (simulado) o lo aplicado. La huella es la del estado previo y es la misma al simular y al aplicar.</summary>
public class ResultadoInyeccionCatalogoDto
{
    public string CodigoApp { get; set; } = string.Empty;
    public bool Simulado { get; set; }
    public string HuellaRemota { get; set; } = string.Empty;
    public List<OperacionCatalogoDto> Operaciones { get; set; } = [];

    /// <summary>Módulos activos del universo de la app que ya no están en el catálogo.</summary>
    public List<string> Sobrantes { get; set; } = [];
}

/// <summary>
/// Una operación. <c>Tipo</c>: CrearModulo, ActualizarModulo, AsociarModulo, CrearPermiso, MoverPermiso o DesactivarModulo.
/// Una reactivación sale con su tipo y <c>Detalle</c> = «reactivar».
/// </summary>
public class OperacionCatalogoDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? CodigoModulo { get; set; }
    public string? Detalle { get; set; }
}
