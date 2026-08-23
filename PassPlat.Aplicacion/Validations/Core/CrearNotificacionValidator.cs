using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class CrearNotificacionValidator : AbstractValidator<CrearNotificacionDto>
{
    public CrearNotificacionValidator()
    {
        RuleFor(x => x.IdUsuario)
            .GreaterThan(0).WithMessage("El usuario es requerido");

        RuleFor(x => x.IdTenant)
            .GreaterThan(0).WithMessage("El tenant es requerido");

        RuleFor(x => x.TipoNotif)
            .NotEmpty().WithMessage("El tipo de notificación es requerido")
            .MaximumLength(50).WithMessage("El tipo de notificación no puede exceder 50 caracteres");

        RuleFor(x => x.Asunto)
            .NotEmpty().WithMessage("El asunto es requerido")
            .MaximumLength(200).WithMessage("El asunto no puede exceder 200 caracteres");

        RuleFor(x => x.Mensaje)
            .MaximumLength(4000).WithMessage("El mensaje no puede exceder 4000 caracteres")
            .When(x => x.Mensaje is not null);
    }
}
