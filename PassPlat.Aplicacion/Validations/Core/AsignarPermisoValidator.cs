using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class AsignarPermisoValidator : AbstractValidator<AsignarPermisoDto>
{
    public AsignarPermisoValidator()
    {
        RuleFor(x => x.IdRol)
            .GreaterThan(0).WithMessage("El rol es requerido");

        RuleFor(x => x.IdPermiso)
            .GreaterThan(0).WithMessage("El permiso es requerido");
    }
}
