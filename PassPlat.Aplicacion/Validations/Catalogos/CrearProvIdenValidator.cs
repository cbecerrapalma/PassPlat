using FluentValidation;
using PassPlat.Aplicacion.Dtos.Catalogos;

namespace PassPlat.Aplicacion.Validations.Catalogos;

public class CrearProvIdenValidator : AbstractValidator<CrearProvIdenDto>
{
    public CrearProvIdenValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50).Matches("^[A-Z0-9_]+$");
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TipoProveedor).InclusiveBetween((byte)1, (byte)10);
        RuleFor(x => x.Protocolo).MaximumLength(20).Must(p => p == null || p is "OAuth" or "OAuth2" or "OIDC" or "SAML2")
            .WithMessage("Protocolo debe ser OAuth, OAuth2, OIDC o SAML2");
        RuleFor(x => x.UrlIssuer).MaximumLength(500).When(x => x.UrlIssuer != null);
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
            RuleFor(x => x.TipoProveedor!.Value).InclusiveBetween((byte)1, (byte)10));
        When(x => x.Protocolo != null, () =>
            RuleFor(x => x.Protocolo).Must(p => p is "OAuth" or "OAuth2" or "OIDC" or "SAML2")
                .WithMessage("Protocolo debe ser OAuth, OAuth2, OIDC o SAML2"));
        When(x => x.UrlIssuer != null, () => RuleFor(x => x.UrlIssuer).MaximumLength(500));
        When(x => x.EndpointAutorizacion != null, () => RuleFor(x => x.EndpointAutorizacion).MaximumLength(500));
        When(x => x.EndpointToken != null, () => RuleFor(x => x.EndpointToken).MaximumLength(500));
        When(x => x.EndpointUserInfo != null, () => RuleFor(x => x.EndpointUserInfo).MaximumLength(500));
        When(x => x.EndpointRevocacion != null, () => RuleFor(x => x.EndpointRevocacion).MaximumLength(500));
    }
}
