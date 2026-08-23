using FluentValidation;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Aplicacion.Validations.Catalogos;

public class CrearDominioTenantValidator : AbstractValidator<CrearDominioTenantDto>
{
    public CrearDominioTenantValidator()
    {
        RuleFor(x => x.IdTenant)
            .GreaterThan(0).WithMessage("El tenant es requerido");

        RuleFor(x => x.Dominio)
            .NotEmpty().WithMessage("El dominio es requerido")
            .MaximumLength(255).WithMessage("El dominio no puede exceder 255 caracteres");
    }
}
