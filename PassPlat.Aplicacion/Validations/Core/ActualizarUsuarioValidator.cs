using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class ActualizarUsuarioValidator : AbstractValidator<ActualizarUsuarioDto>
{
    public ActualizarUsuarioValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("El usuario es requerido");

        RuleFor(x => x.Nombre)
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres")
            .When(x => x.Nombre is not null);

        RuleFor(x => x.Apellido)
            .MaximumLength(100).WithMessage("El apellido no puede exceder 100 caracteres")
            .When(x => x.Apellido is not null);
    }
}
