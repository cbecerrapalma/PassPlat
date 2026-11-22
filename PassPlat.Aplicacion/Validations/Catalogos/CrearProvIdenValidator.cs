using FluentValidation;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Aplicacion.Validations.Catalogos;

public class CrearProvIdenValidator : AbstractValidator<CrearProvIdenDto>
{
    public CrearProvIdenValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50).Matches("^[A-Z0-9_]+$");
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TipoProveedor).Equal((byte)1).WithMessage("TipoProveedor debe ser 1 (OAuth2)");
        RuleFor(x => x.Protocolo).MaximumLength(20).Must(p => p == null || p == "OAuth")
            .WithMessage("Protocolo debe ser OAuth");
        RuleFor(x => x.EndpointAutorizacion).MaximumLength(500);
        RuleFor(x => x.EndpointToken).MaximumLength(500);
        RuleFor(x => x.EndpointUserInfo).MaximumLength(500);
        RuleFor(x => x.EndpointRevocacion).MaximumLength(500);
    }
}

public class ActualizarProvIdenValidator : AbstractValidator<ActualizarProvIdenDto>
{
    public ActualizarProvIdenValidator()
    {
        When(x => x.TipoProveedor.HasValue, () =>
            RuleFor(x => x.TipoProveedor!.Value).Equal((byte)1).WithMessage("TipoProveedor debe ser 1 (OAuth2)"));
        When(x => x.Protocolo != null, () =>
            RuleFor(x => x.Protocolo).Must(p => p == "OAuth")
                .WithMessage("Protocolo debe ser OAuth"));
        When(x => x.EndpointAutorizacion != null, () => RuleFor(x => x.EndpointAutorizacion).MaximumLength(500));
        When(x => x.EndpointToken != null, () => RuleFor(x => x.EndpointToken).MaximumLength(500));
        When(x => x.EndpointUserInfo != null, () => RuleFor(x => x.EndpointUserInfo).MaximumLength(500));
        When(x => x.EndpointRevocacion != null, () => RuleFor(x => x.EndpointRevocacion).MaximumLength(500));
    }
}
