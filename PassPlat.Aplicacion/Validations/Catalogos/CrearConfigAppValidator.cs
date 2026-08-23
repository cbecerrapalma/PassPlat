using FluentValidation;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Aplicacion.Validations.Catalogos;

public class CrearConfigAppValidator : AbstractValidator<CrearConfigAppDto>
{
    public CrearConfigAppValidator()
    {
        RuleFor(x => x.Grupo)
            .NotEmpty().WithMessage("El grupo es requerido")
            .MaximumLength(50).WithMessage("El grupo no puede exceder 50 caracteres");

        RuleFor(x => x.Clave)
            .NotEmpty().WithMessage("La clave es requerida")
            .MaximumLength(100).WithMessage("La clave no puede exceder 100 caracteres");

        RuleFor(x => x.Valor)
            .NotEmpty().WithMessage("El valor es requerido");

        RuleFor(x => x.Tipo)
            .NotEmpty().WithMessage("El tipo es requerido")
            .Must(t => t is "string" or "int" or "bool" or "json" or "encrypted")
            .WithMessage("El tipo debe ser: string, int, bool, json o encrypted");

        RuleFor(x => x.Descripcion)
            .MaximumLength(255).WithMessage("La descripción no puede exceder 255 caracteres")
            .When(x => x.Descripcion is not null);
    }
}
