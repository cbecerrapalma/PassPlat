using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class CrearDispConfiableValidator : AbstractValidator<CrearDispConfiableDto>
{
    public CrearDispConfiableValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El usuario es requerido");

        RuleFor(x => x.IdTenant)
            .GreaterThan(0).WithMessage("El tenant es requerido");

        RuleFor(x => x.IdDisp)
            .GreaterThan(0).WithMessage("El dispositivo es requerido");

        RuleFor(x => x.Nombre)
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres")
            .When(x => x.Nombre is not null);
    }
}
