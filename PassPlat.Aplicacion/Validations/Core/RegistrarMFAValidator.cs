using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class RegistrarMFAValidator : AbstractValidator<RegistrarMFADto>
{
    public RegistrarMFAValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El usuario es requerido");

        RuleFor(x => x.IdTenant)
            .GreaterThan(0).WithMessage("El tenant es requerido");

        RuleFor(x => x.IdTipoMFA)
            .GreaterThan(0).WithMessage("El tipo MFA es requerido");

        RuleFor(x => x.IdEstado)
            .GreaterThan(0).WithMessage("El estado MFA es requerido");

        RuleFor(x => x.IdMFA)
            .NotEmpty().WithMessage("El identificador MFA es requerido")
            .MaximumLength(500).WithMessage("El identificador MFA no puede exceder 500 caracteres");

        RuleFor(x => x.ClavePublica)
            .MaximumLength(2000).WithMessage("La clave pública no puede exceder 2000 caracteres")
            .When(x => x.ClavePublica is not null);

        RuleFor(x => x.Metadatos)
            .MaximumLength(4000).WithMessage("Los metadatos no pueden exceder 4000 caracteres")
            .When(x => x.Metadatos is not null);
    }
}
