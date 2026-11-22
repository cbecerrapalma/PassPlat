using CBP.Data.Utilities.Helpers;
using Xunit;

namespace PassPlat.Aplicacion.Tests.Tests.S66;

public class S66RedaccionSqlTests
{
    [Fact]
    public void Redactar_MensajeIngles_ConservaIndiceYEliminaValor()
    {
        // Arrange
        var mensaje = "Cannot insert duplicate key row in object 'dbo.Usuarios' with unique index 'UX_Usuarios_NomUsuario'. The duplicate key value is (x_prueba).";

        // Act
        var resultado = SqlErrorRedactor.Redact(mensaje);

        // Assert
        Assert.Contains("UX_Usuarios_NomUsuario", resultado);
        Assert.DoesNotContain("x_prueba", resultado);
        Assert.Contains("(<redactado>)", resultado);
    }

    [Fact]
    public void Redactar_MensajeEspanol_EliminaValor()
    {
        // Arrange
        var mensaje = "El valor de la clave duplicada es (usuario_duplicado).";

        // Act
        var resultado = SqlErrorRedactor.Redact(mensaje);

        // Assert
        Assert.DoesNotContain("usuario_duplicado", resultado);
        Assert.Contains("(<redactado>)", resultado);
    }

    [Fact]
    public void Redactar_Null_DevuelveVacio()
    {
        // Act
        var resultado = SqlErrorRedactor.Redact(null);

        // Assert
        Assert.Equal(string.Empty, resultado);
    }

    [Fact]
    public void Redactar_SinPatron_NoCambia()
    {
        // Arrange
        var mensaje = "Error de conexión timeout";

        // Act
        var resultado = SqlErrorRedactor.Redact(mensaje);

        // Assert
        Assert.Equal(mensaje, resultado);
    }

    [Fact]
    public void NumeroSql_ExcepcionSinSql_DevuelveNull()
    {
        // Arrange
        var ex = new Exception("Error genérico");

        // Act
        var resultado = SqlErrorRedactor.GetErrorNumber(ex);

        // Assert
        Assert.Null(resultado);
    }
}