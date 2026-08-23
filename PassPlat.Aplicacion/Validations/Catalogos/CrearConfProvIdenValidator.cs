using FluentValidation;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;

namespace PassPlat.Aplicacion.Validations.Catalogos;

public class CrearConfProvIdenValidator : AbstractValidator<CrearConfProvIdenDto>
{
    public CrearConfProvIdenValidator(IProvIdenService provIdenService)
    {
        RuleFor(x => x.IdTenant).GreaterThan(0);
        RuleFor(x => x.IdProvIden).GreaterThan(0);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ClientSecret).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Callback).NotEmpty().MaximumLength(500);
        RuleFor(x => x.RedirectUri).MaximumLength(500);
        RuleFor(x => x.Scopes).MaximumLength(500);
        RuleFor(x => x.Estado).InclusiveBetween((byte)0, (byte)2);
        When(x => x.RolDefecto.HasValue, () => RuleFor(x => x.RolDefecto!.Value).GreaterThan(0));

        WhenAsync(async (dto, ct) => await EsGoogle(dto.IdProvIden, provIdenService, ct), () =>
        {
            RuleFor(x => x.Callback)
                .Must(uri => uri.StartsWith("https://"))
                .WithMessage("Callback debe comenzar con https://");
            RuleFor(x => x.ClientId)
                .Must(id => id.EndsWith(".apps.googleusercontent.com"))
                .WithMessage("ClientId debe terminar en .apps.googleusercontent.com");
            RuleFor(x => x.Scopes)
                .Must(s => !string.IsNullOrWhiteSpace(s) && s.Contains("openid") && s.Contains("profile") && s.Contains("email"))
                .WithMessage("Scopes debe contener openid, profile y email");
        });
    }

    private static async Task<bool> EsGoogle(int idProvIden, IProvIdenService service, CancellationToken ct)
    {
        var result = await service.ObtenerPorCodigoAsync("GOOGLE", ct);
        return result.IsSuccess && result.Value?.Id == idProvIden;
    }
}

public class ActualizarConfProvIdenValidator : AbstractValidator<ActualizarConfProvIdenDto>
{
    public ActualizarConfProvIdenValidator()
    {
        When(x => x.ClientId != null, () => RuleFor(x => x.ClientId).MaximumLength(500));
        When(x => x.ClientSecret != null, () => RuleFor(x => x.ClientSecret).MaximumLength(1000));
        When(x => x.Callback != null, () =>
        {
            RuleFor(x => x.Callback).MaximumLength(500);
            RuleFor(x => x.Callback).Must(uri => uri is not null && uri.StartsWith("https://"))
                .WithMessage("Callback debe comenzar con https://");
        });
        When(x => x.RedirectUri != null, () =>
        {
            RuleFor(x => x.RedirectUri).MaximumLength(500);
            RuleFor(x => x.RedirectUri).Must(uri => uri is not null && uri.StartsWith("https://"))
                .WithMessage("RedirectUri debe comenzar con https://");
        });
        When(x => x.Scopes != null, () => RuleFor(x => x.Scopes).MaximumLength(500));
        When(x => x.Estado.HasValue, () => RuleFor(x => x.Estado!.Value).InclusiveBetween((byte)0, (byte)2));
        When(x => x.RolDefecto.HasValue, () => RuleFor(x => x.RolDefecto!.Value).GreaterThan(0));
    }
}
