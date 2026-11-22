using MudBlazor;

namespace PassPlat.Web.Shared;

/// <summary>
/// S58.1 · D3 — tamaño de los diálogos de formulario.
/// </summary>
public enum TamanoDialogo
{
    /// <summary>Hasta 4 campos.</summary>
    Compacto,

    /// <summary>De 5 a 12 campos.</summary>
    Estandar,

    /// <summary>Más de 12 campos, editores de código o HTML, o tablas anchas.</summary>
    Amplio
}

/// <summary>
/// S58.1 · D3 — opciones comunes de los diálogos de formulario: todos se abren a
/// pantalla completa con un ancho minimo comodo y el contenido con scroll propio.
/// </summary>
public static class OpcionesDialogo
{
    /// <summary>Ancho maximo segun el tamano.</summary>
    public static MaxWidth MaxWidthDe(TamanoDialogo tamano) => tamano switch
    {
        TamanoDialogo.Compacto => MaxWidth.Small,
        TamanoDialogo.Estandar => MaxWidth.Medium,
        _ => MaxWidth.Large
    };

    /// <summary>Opciones de apertura (DialogOptions) de un diálogo de formulario.</summary>
    public static DialogOptions Dialogo(TamanoDialogo tamano) => new()
    {
        MaxWidth = MaxWidthDe(tamano),
        FullWidth = true,
        CloseButton = true
    };

    /// <summary>Tamano segun el numero de campos: hasta 4 compacto, de 5 a 12 estandar, mas de 12 amplio.</summary>
    public static TamanoDialogo SegunCampos(int campos) => campos switch
    {
        <= 4 => TamanoDialogo.Compacto,
        <= 12 => TamanoDialogo.Estandar,
        _ => TamanoDialogo.Amplio
    };
}
