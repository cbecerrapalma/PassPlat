using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class CrearIdenExtValidator : AbstractValidator<CrearIdenExtDto>
{
    public CrearIdenExtValidator()
    {
        RuleFor(x => x.IdUsuario).GreaterThan(0);
        RuleFor(x => x.IdProvIden).GreaterThan(0);
        RuleFor(x => x.IdTenant).GreaterThan(0);
        RuleFor(x => x.SubExterno).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ProviderUserName).MaximumLength(255);
        RuleFor(x => x.EmailExterno).MaximumLength(255).EmailAddress().When(x => !string.IsNullOrEmpty(x.EmailExterno));
        RuleFor(x => x.NombreExterno).MaximumLength(255);
        RuleFor(x => x.Avatar).MaximumLength(500);
    }
}
