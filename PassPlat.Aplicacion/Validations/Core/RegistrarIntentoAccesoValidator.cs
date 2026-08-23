using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class RegistrarIntentoAccesoValidator : AbstractValidator<RegistrarIntentoAccesoDto>
{
    public RegistrarIntentoAccesoValidator()
    {
        RuleFor(x => x.NomUsuarioIntentado)
            .NotEmpty().WithMessage("El nombre de usuario es requerido")
            .MaximumLength(100).WithMessage("El nombre de usuario no puede exceder 100 caracteres");

        RuleFor(x => x.IdResultado)
            .GreaterThan(0).WithMessage("El resultado es requerido");
    }
}
