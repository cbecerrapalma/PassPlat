using CBP.Results.Errors;
using PassPlat.Aplicacion.Errors;

namespace PassPlat.Aplicacion.Tests.Tests.S64;

/// <summary>S64: la tabla central asigna a cada código de negocio su ErrorType; los técnicos quedan en 500.</summary>
public class S64ClasificacionErroresTests
{
    [Theory]
    [InlineData("ACCESO_DUPLICADO", ErrorType.Conflict)]
    [InlineData("CODIGO_DUPLICADO", ErrorType.Conflict)]
    [InlineData("YA_ASIGNADO", ErrorType.Conflict)]
    [InlineData("YA_EXISTE", ErrorType.Conflict)]
    [InlineData("TENANT_SISTEMA", ErrorType.Conflict)]
    [InlineData("USUARIO_NO_ENCONTRADO", ErrorType.NotFound)]
    [InlineData("NO_ENCONTRADO", ErrorType.NotFound)]
    [InlineData("ACCESO_NOT_FOUND", ErrorType.NotFound)]
    [InlineData("ROL_INACTIVO", ErrorType.Validation)]
    [InlineData("SIN_MEMBRESIA_TENANT", ErrorType.Validation)]
    [InlineData("LOGIN_FAILED", ErrorType.Unauthorized)]
    [InlineData("SESION_REVOCADA", ErrorType.Unauthorized)]
    [InlineData("SIN_ACCESO_APP", ErrorType.Forbidden)]
    [InlineData("PWD_EXPIRADA", ErrorType.Forbidden)]
    [InlineData("TENANT_REQUIRED", ErrorType.Forbidden)]
    public void Codigo_de_negocio_tiene_su_tipo(string codigo, ErrorType esperado)
    {
        Assert.Equal(esperado, ClasificacionErrores.TipoPorCodigo(codigo));
    }

    [Theory]
    [InlineData("DB_ERROR")]
    [InlineData("SP_NO_RESULT")]
    [InlineData("VALIDATION_ERROR")]
    [InlineData("PROVIDER_ERROR")]
    [InlineData("KEY_NOT_FOUND")]
    public void Codigo_tecnico_sigue_en_500(string codigo)
    {
        Assert.Null(ClasificacionErrores.TipoPorCodigo(codigo));
        Assert.Contains(codigo, ClasificacionErrores.Tecnicos);
    }

    [Theory]
    [InlineData("CODIGO_QUE_NO_EXISTE")]
    [InlineData("")]
    [InlineData(null)]
    public void Codigo_desconocido_no_se_clasifica(string? codigo)
    {
        Assert.Null(ClasificacionErrores.TipoPorCodigo(codigo));
    }

    [Fact]
    public void Ningun_codigo_es_de_negocio_y_tecnico_a_la_vez()
    {
        var conocidos = ClasificacionErrores.Conocidos.ToList();
        Assert.Equal(conocidos.Count, conocidos.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ClasificacionErrores.Tecnicos, c => Assert.Null(ClasificacionErrores.TipoPorCodigo(c)));
    }
}
