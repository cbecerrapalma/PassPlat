using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class CrearPoliticaPwdValidator : AbstractValidator<CrearPoliticaPwdDto>
{
    public CrearPoliticaPwdValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("El código es requerido")
            .MaximumLength(50).WithMessage("El código no puede exceder 50 caracteres");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres");

        RuleFor(x => x.LongMin)
            .InclusiveBetween((byte)0, (byte)128)
            .WithMessage("La longitud mínima debe estar entre 0 y 128");

        RuleFor(x => x.LongMax)
            .InclusiveBetween((byte)1, (byte)255)
            .WithMessage("La longitud máxima debe estar entre 1 y 255")
            .GreaterThanOrEqualTo(x => x.LongMin)
            .WithMessage("La longitud máxima debe ser mayor o igual a la mínima");

        RuleFor(x => x.DiasVigencia)
            .InclusiveBetween((short)0, (short)3650)
            .WithMessage("La vigencia debe estar entre 0 y 3650 días");

        RuleFor(x => x.DurBloqueoMin)
            .GreaterThanOrEqualTo(0).WithMessage("La duración de bloqueo no puede ser negativa");

        RuleFor(x => x.CaracteresEspeciales)
            .MaximumLength(100).WithMessage("Los caracteres especiales no pueden exceder 100")
            .When(x => x.CaracteresEspeciales is not null);
    }
}
