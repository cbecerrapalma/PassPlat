using FluentValidation.TestHelper;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Validations.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S78;

public class EnviarEmailPlantillaValidatorTests
{
    private readonly EnviarEmailPlantillaValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = new EnviarEmailPlantillaRequest(
            CodigoPlantilla: "inventa-store-contacto",
            Destinatario: "test@example.com",
            NombreDestinatario: "Juan Pérez",
            Variables: new Dictionary<string, string> { ["NombreTienda"] = "Mi Tienda" },
            Cultura: "es");

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "Código de plantilla vacío")]
    [InlineData("INVALID_CODE", "Mayúsculas no permitidas")]
    [InlineData("valid_code_with_underscore", "Guión bajo no permitido en código")]
    public void InvalidCodigoPlantilla_FailsValidation(string codigo, string _)
    {
        var request = new EnviarEmailPlantillaRequest(
            CodigoPlantilla: codigo,
            Destinatario: "test@example.com",
            Cultura: "es");

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CodigoPlantilla);
    }

    [Theory]
    [InlineData("", "Email vacío")]
    [InlineData("not-an-email", "Email inválido")]
    [InlineData("a@", "Email sin dominio")]
    public void InvalidDestinatario_FailsValidation(string email, string _)
    {
        var request = new EnviarEmailPlantillaRequest(
            CodigoPlantilla: "inventa-store-contacto",
            Destinatario: email,
            Cultura: "es");

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Destinatario);
    }

    [Fact]
    public void NombreDestinatario_TooLong_FailsValidation()
    {
        var request = new EnviarEmailPlantillaRequest(
            CodigoPlantilla: "inventa-store-contacto",
            Destinatario: "test@example.com",
            NombreDestinatario: new string('a', 201),
            Cultura: "es");

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NombreDestinatario);
    }

    [Fact]
    public void Cultura_NotEs_FailsValidation()
    {
        var request = new EnviarEmailPlantillaRequest(
            CodigoPlantilla: "inventa-store-contacto",
            Destinatario: "test@example.com",
            Cultura: "en");

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Cultura);
    }

    [Fact]
    public void Variables_TooMany_FailsValidation()
    {
        var variables = Enumerable.Range(1, 21).ToDictionary(i => $"Var{i}", i => "value");
        var request = new EnviarEmailPlantillaRequest(
            CodigoPlantilla: "inventa-store-contacto",
            Destinatario: "test@example.com",
            Variables: variables,
            Cultura: "es");

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Variables);
    }

    [Fact]
    public void VariableKey_InvalidFormat_FailsValidation()
    {
        var request = new EnviarEmailPlantillaRequest(
            CodigoPlantilla: "inventa-store-contacto",
            Destinatario: "test@example.com",
            Variables: new Dictionary<string, string> { ["1InvalidKey"] = "value" },
            Cultura: "es");

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Variables[0].Key");
    }

    [Fact]
    public void VariableValue_TooLong_FailsValidation()
    {
        var request = new EnviarEmailPlantillaRequest(
            CodigoPlantilla: "inventa-store-contacto",
            Destinatario: "test@example.com",
            Variables: new Dictionary<string, string> { ["ValidKey"] = new string('a', 4001) },
            Cultura: "es");

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Variables[0].Value");
    }
}
