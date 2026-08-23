using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class CrearSesionValidator : AbstractValidator<CrearSesionDto>
{
    public CrearSesionValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El usuario es requerido");

        RuleFor(x => x.IdTenant)
            .GreaterThan(0).WithMessage("El tenant es requerido");

        RuleFor(x => x.IdApp)
            .GreaterThan(0).WithMessage("La aplicación es requerida");

        RuleFor(x => x.IdTokenExt)
            .NotEmpty().WithMessage("El token externo es requerido")
            .MaximumLength(500).WithMessage("El token externo no puede exceder 500 caracteres");

        RuleFor(x => x.FecExpira)
            .GreaterThan(DateTime.Now).WithMessage("La fecha de expiración debe ser futura");

        RuleFor(x => x.HashRefresh)
            .MaximumLength(500).WithMessage("El hash refresh no puede exceder 500 caracteres")
            .When(x => x.HashRefresh is not null);
    }
}
