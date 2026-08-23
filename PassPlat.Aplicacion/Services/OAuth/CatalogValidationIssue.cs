using PassPlat.Dominio.Enums;

namespace PassPlat.Aplicacion.Services.OAuth;

public sealed record CatalogValidationIssue(
    string Code,
    string Description,
    ESeveridadValidacion Severity);
