using FluentValidation;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Aplicacion.Validations.Catalogos;

public class CrearRolValidator : AbstractValidator<CrearRolDto>
{
    public CrearRolValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("El código es requerido")
            .MaximumLength(20).WithMessage("El código no puede exceder 20 caracteres");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(50).WithMessage("El nombre no puede exceder 50 caracteres");

        RuleFor(x => x.Descripcion)
            .MaximumLength(200).WithMessage("La descripción no puede exceder 200 caracteres")
            .When(x => x.Descripcion is not null);

        RuleFor(x => x.IdTenant)
            .GreaterThan(0).WithMessage("El tenant es requerido")
            .When(x => x.IdTenant.HasValue);

        RuleFor(x => x.IdPolitica)
            .GreaterThan(0).WithMessage("La política no es válida")
            .When(x => x.IdPolitica.HasValue);
    }
}
