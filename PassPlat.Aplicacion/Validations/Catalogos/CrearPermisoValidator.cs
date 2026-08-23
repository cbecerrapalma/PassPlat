using FluentValidation;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Aplicacion.Validations.Catalogos;

public class CrearPermisoValidator : AbstractValidator<CrearPermisoDto>
{
    public CrearPermisoValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("El código es requerido")
            .MaximumLength(50).WithMessage("El código no puede exceder 50 caracteres");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres");

        RuleFor(x => x.Descripcion)
            .MaximumLength(200).WithMessage("La descripción no puede exceder 200 caracteres")
            .When(x => x.Descripcion is not null);

        RuleFor(x => x.IdModulo)
            .GreaterThan(0).WithMessage("El módulo es requerido");
    }
}
