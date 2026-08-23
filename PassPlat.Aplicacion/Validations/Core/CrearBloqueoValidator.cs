using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class CrearBloqueoValidator : AbstractValidator<CrearBloqueoDto>
{
    public CrearBloqueoValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El usuario es requerido");

        RuleFor(x => x.IdTenant)
            .GreaterThan(0).WithMessage("El tenant es requerido");

        RuleFor(x => x.IdTipoBloqueo)
            .GreaterThan(0).WithMessage("El tipo de bloqueo es requerido");

        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("El motivo es requerido")
            .MaximumLength(500).WithMessage("El motivo no puede exceder 500 caracteres");

        RuleFor(x => x.TipoDeteccion)
            .MaximumLength(100).WithMessage("El tipo de detección no puede exceder 100 caracteres")
            .When(x => x.TipoDeteccion is not null);
    }
}
