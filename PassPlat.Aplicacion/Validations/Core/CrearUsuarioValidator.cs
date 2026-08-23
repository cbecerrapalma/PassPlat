using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class CrearUsuarioValidator : AbstractValidator<CrearUsuarioDto>
{
    public CrearUsuarioValidator()
    {
        RuleFor(x => x.IdTenant)
            .GreaterThan(0).WithMessage("El tenant es requerido");

        RuleFor(x => x.IdEstado)
            .GreaterThan(0).WithMessage("El estado es requerido");

        RuleFor(x => x.NomUsuario)
            .NotEmpty().WithMessage("El nombre de usuario es requerido")
            .MaximumLength(100).WithMessage("El nombre de usuario no puede exceder 100 caracteres");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
                .WithMessage("El email no es válido")
            .MaximumLength(255).When(x => !string.IsNullOrWhiteSpace(x.Email))
                .WithMessage("El email no puede exceder 255 caracteres");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres");

        RuleFor(x => x.Apellido)
            .NotEmpty().WithMessage("El apellido es requerido")
            .MaximumLength(100).WithMessage("El apellido no puede exceder 100 caracteres");

        When(x => x.Password is not null, () =>
        {
            RuleFor(x => x.Password!)
                .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres")
                .MaximumLength(128).WithMessage("La contraseña no puede exceder 128 caracteres");
        });
    }
}
