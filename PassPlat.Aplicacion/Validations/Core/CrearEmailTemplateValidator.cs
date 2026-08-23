using FluentValidation;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Validations.Core;

public class CrearEmailTemplateValidator : AbstractValidator<CrearEmailTemplateDto>
{
    public CrearEmailTemplateValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la plantilla es requerido")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres")
            .Matches(@"^[a-z0-9\-_]+$").WithMessage("El nombre solo acepta minúsculas, números, guiones y guión bajo");

        RuleFor(x => x.Cultura)
            .NotEmpty().WithMessage("La cultura es requerida")
            .MaximumLength(10).WithMessage("La cultura no puede exceder 10 caracteres");

        RuleFor(x => x.Asunto)
            .NotEmpty().WithMessage("El asunto es requerido")
            .MaximumLength(500).WithMessage("El asunto no puede exceder 500 caracteres");

        RuleFor(x => x.CuerpoHtml)
            .NotEmpty().WithMessage("El cuerpo HTML es requerido");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500).WithMessage("La descripción no puede exceder 500 caracteres")
            .When(x => x.Descripcion is not null);

        RuleFor(x => x.Categoria)
            .NotEmpty().WithMessage("La categoría es requerida")
            .MaximumLength(50).WithMessage("La categoría no puede exceder 50 caracteres");
    }
}

public class ActualizarEmailTemplateValidator : AbstractValidator<ActualizarEmailTemplateDto>
{
    public ActualizarEmailTemplateValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("El ID de plantilla es requerido");

        RuleFor(x => x.Asunto)
            .MaximumLength(500).WithMessage("El asunto no puede exceder 500 caracteres")
            .When(x => x.Asunto is not null);

        RuleFor(x => x.Descripcion)
            .MaximumLength(500).WithMessage("La descripción no puede exceder 500 caracteres")
            .When(x => x.Descripcion is not null);

        RuleFor(x => x.Categoria)
            .MaximumLength(50).WithMessage("La categoría no puede exceder 50 caracteres")
            .When(x => x.Categoria is not null);
    }
}

public class PublicarTemplateValidator : AbstractValidator<PublicarTemplateDto>
{
    public PublicarTemplateValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("El ID de plantilla es requerido");
    }
}
