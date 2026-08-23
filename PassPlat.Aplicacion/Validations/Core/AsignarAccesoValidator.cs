using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class AsignarAccesoValidator : AbstractValidator<AsignarAccesoDto>
{
    public AsignarAccesoValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El usuario es requerido");

        RuleFor(x => x.IdTenant)
            .GreaterThan(0).WithMessage("El tenant es requerido");

        RuleFor(x => x.IdApp)
            .GreaterThan(0).WithMessage("La aplicación es requerida");

        RuleFor(x => x.IdRol)
            .GreaterThan(0).WithMessage("El rol es requerido");
    }
}
