using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class CrearEmailTemplatePartialValidator : AbstractValidator<CrearEmailTemplatePartialDto>
{
    public CrearEmailTemplatePartialValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del partial es requerido")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres")
            .Matches(@"^[a-z0-9\-_]+$").WithMessage("El nombre solo acepta minúsculas, números, guiones y guión bajo");

        RuleFor(x => x.CuerpoHtml)
            .NotEmpty().WithMessage("El cuerpo HTML es requerido");
    }
}

public class ActualizarEmailTemplatePartialValidator : AbstractValidator<ActualizarEmailTemplatePartialDto>
{
    public ActualizarEmailTemplatePartialValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("El ID del partial es requerido");
    }
}
