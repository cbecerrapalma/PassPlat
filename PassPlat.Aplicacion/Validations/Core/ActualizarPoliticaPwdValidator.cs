using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class ActualizarPoliticaPwdValidator : AbstractValidator<ActualizarPoliticaPwdDto>
{
    public ActualizarPoliticaPwdValidator()
    {
        RuleFor(x => x.Nombre)
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres")
            .When(x => x.Nombre is not null);

        RuleFor(x => x.LongMin)
            .InclusiveBetween((byte)0, (byte)128)
            .When(x => x.LongMin.HasValue);

        RuleFor(x => x.LongMax)
            .InclusiveBetween((byte)1, (byte)255)
            .When(x => x.LongMax.HasValue);

        RuleFor(x => x.CaracteresEspeciales)
            .MaximumLength(100).WithMessage("Los caracteres especiales no pueden exceder 100")
            .When(x => x.CaracteresEspeciales is not null);

        RuleFor(x => x.DiasVigencia)
            .InclusiveBetween((short)0, (short)3650)
            .When(x => x.DiasVigencia.HasValue);

        RuleFor(x => x.DurBloqueoMin)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DurBloqueoMin.HasValue);
    }
}
