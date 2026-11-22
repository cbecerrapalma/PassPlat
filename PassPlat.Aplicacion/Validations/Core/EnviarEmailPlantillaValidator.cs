using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

// S78: validador para el endpoint de envío de email por plantilla (apps externas)
public class EnviarEmailPlantillaValidator : AbstractValidator<EnviarEmailPlantillaRequest>
{
    public EnviarEmailPlantillaValidator()
    {
        RuleFor(x => x.CodigoPlantilla)
            .NotEmpty().WithMessage("El código de plantilla es requerido")
            .MaximumLength(100).WithMessage("El código no puede exceder 100 caracteres")
            .Matches(@"^[a-z0-9\-]+$").WithMessage("El código solo acepta minúsculas, números y guiones");

        RuleFor(x => x.Destinatario)
            .NotEmpty().WithMessage("El destinatario es requerido")
            .EmailAddress().WithMessage("El destinatario debe ser un email válido")
            .MaximumLength(254).WithMessage("El email no puede exceder 254 caracteres");

        RuleFor(x => x.NombreDestinatario)
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres")
            .When(x => x.NombreDestinatario is not null);

        RuleFor(x => x.Cultura)
            .NotEmpty().WithMessage("La cultura es requerida")
            .MaximumLength(10).WithMessage("La cultura no puede exceder 10 caracteres")
            .Equal("es").WithMessage("Solo se soporta cultura 'es'");

        RuleFor(x => x.Variables)
            .Must(v => v == null || v.Count <= 20)
            .WithMessage("Máximo 20 variables permitidas")
            .When(x => x.Variables is not null);

        When(x => x.Variables is not null, () =>
        {
            RuleForEach(x => x.Variables!)
                .ChildRules(kv =>
                {
                    kv.RuleFor(x => x.Key)
                        .NotEmpty().WithMessage("La clave de variable no puede ser vacía")
                        .MaximumLength(50).WithMessage("La clave no puede exceder 50 caracteres")
                        .Matches(@"^[A-Za-z][A-Za-z0-9_]{0,49}$").WithMessage("La clave debe empezar con letra y contener solo letras, números y guión bajo");
                    kv.RuleFor(x => x.Value)
                        .MaximumLength(4000).WithMessage("El valor no puede exceder 4000 caracteres");
                });
        });
    }
}