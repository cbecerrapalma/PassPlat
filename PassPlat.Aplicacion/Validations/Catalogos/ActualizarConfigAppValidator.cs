using FluentValidation;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Aplicacion.Validations.Catalogos;

public class ActualizarConfigAppValidator : AbstractValidator<ActualizarConfigAppDto>
{
    public ActualizarConfigAppValidator()
    {
        RuleFor(x => x.Valor)
            .NotEmpty().WithMessage("El valor es requerido");

        RuleFor(x => x.Descripcion)
            .MaximumLength(255).WithMessage("La descripción no puede exceder 255 caracteres")
            .When(x => x.Descripcion is not null);
    }
}
