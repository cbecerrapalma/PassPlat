using FluentValidation;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Aplicacion.Validations.Catalogos;

public class CrearAppValidator : AbstractValidator<CrearAppDto>
{
    public CrearAppValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("El código es requerido")
            .MaximumLength(50).WithMessage("El código no puede exceder 50 caracteres");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres");

        RuleFor(x => x.UrlBase)
            .MaximumLength(255).WithMessage("La URL no puede exceder 255 caracteres")
            .When(x => x.UrlBase is not null);
    }
}
