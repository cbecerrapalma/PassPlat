using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class RegistrarAuditoriaPwdValidator : AbstractValidator<RegistrarAuditoriaPwdDto>
{
    public RegistrarAuditoriaPwdValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El usuario es requerido");

        RuleFor(x => x.IdTipoAccion)
            .GreaterThan(0).WithMessage("El tipo de acción es requerido");
    }
}
