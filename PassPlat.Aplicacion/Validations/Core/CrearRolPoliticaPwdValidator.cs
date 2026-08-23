using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class CrearRolPoliticaPwdValidator : AbstractValidator<CrearRolPoliticaPwdDto>
{
    public CrearRolPoliticaPwdValidator()
    {
        RuleFor(x => x.IdTenant)
            .GreaterThan(0).WithMessage("El tenant es requerido");

        RuleFor(x => x.IdRol)
            .GreaterThan(0).WithMessage("El rol es requerido");

        RuleFor(x => x.IdPolitica)
            .GreaterThan(0).WithMessage("La política es requerida");
    }
}
