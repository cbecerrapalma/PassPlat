using FluentValidation;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Aplicacion.Validations.Catalogos;

public class ActualizarRolValidator : AbstractValidator<ActualizarRolDto>
{
    public ActualizarRolValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(50).WithMessage("El nombre no puede exceder 50 caracteres");

        RuleFor(x => x.Descripcion)
            .MaximumLength(200).WithMessage("La descripción no puede exceder 200 caracteres")
            .When(x => x.Descripcion is not null);
    }
}
