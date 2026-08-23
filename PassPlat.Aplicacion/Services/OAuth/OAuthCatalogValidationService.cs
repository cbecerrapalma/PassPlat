using CBP.Results;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Constants;
using PassPlat.Dominio.Enums;

namespace PassPlat.Aplicacion.Services.OAuth;

public interface IOAuthCatalogValidationService
{
    Task<Result<IReadOnlyList<CatalogValidationIssue>>> ValidateAsync(CancellationToken ct = default);
}

public class OAuthCatalogValidationService : IOAuthCatalogValidationService
{
    private readonly IProvIdenRepository _provIdenRepo;
    private readonly IConfProvIdenRepository _confProvIdenRepo;

    public OAuthCatalogValidationService(IProvIdenRepository provIdenRepo, IConfProvIdenRepository confProvIdenRepo)
    {
        _provIdenRepo = provIdenRepo;
        _confProvIdenRepo = confProvIdenRepo;
    }

    public async Task<Result<IReadOnlyList<CatalogValidationIssue>>> ValidateAsync(CancellationToken ct = default)
    {
        var issues = new List<CatalogValidationIssue>();

        try
        {
            var proveedoresResult = await _provIdenRepo.ObtenerTodosOrdenadosAsync(ct);
            if (proveedoresResult.IsFailure)
                return Result<IReadOnlyList<CatalogValidationIssue>>.Failure(proveedoresResult.Error!);
            var proveedores = proveedoresResult.Value;

            // 1. Cantidad correcta
            if (proveedores.Count != OAuthProviders.Todos.Length)
                issues.Add(new CatalogValidationIssue(
                    "CANTIDAD_INCORRECTA",
                    $"Se esperaban {OAuthProviders.Todos.Length} proveedores, se encontraron {proveedores.Count}",
                    ESeveridadValidacion.Error));

            // 2. Códigos duplicados
            var duplicados = proveedores.GroupBy(p => p.Codigo).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            foreach (var dup in duplicados)
                issues.Add(new CatalogValidationIssue(
                    "CODIGOS_DUPLICADOS",
                    $"Código duplicado: {dup}",
                    ESeveridadValidacion.Error));

            // 3. Protocolo OAuth
            foreach (var p in proveedores)
            {
                if (!string.Equals(p.Protocolo, "OAuth", StringComparison.OrdinalIgnoreCase))
                    issues.Add(new CatalogValidationIssue(
                        "PROTOCOLO_INVALIDO",
                        $"El proveedor {p.Codigo} tiene Protocolo='{p.Protocolo ?? "(nulo)"}', se esperaba 'OAuth'",
                        ESeveridadValidacion.Error));
            }

            // 4-5. Cada código debe estar en OAuthProviders.Todos
            var codigosValidos = new HashSet<string>(OAuthProviders.Todos, StringComparer.OrdinalIgnoreCase);
            foreach (var p in proveedores)
            {
                if (!codigosValidos.Contains(p.Codigo))
                    issues.Add(new CatalogValidationIssue(
                        "CODIGO_INVALIDO",
                        $"El proveedor '{p.Codigo}' no está en la lista de proveedores soportados",
                        ESeveridadValidacion.Error));
            }

            // 6. Endpoints HTTPS
            var endpoints = new (string campo, string? valor)[]
            {
                ("Authorization", null),
                ("Token", null),
                ("UserInfo", null),
                ("JWKS", null),
                ("Revocación", null)
            };
            foreach (var p in proveedores)
            {
                var vals = new (string campo, string? valor)[]
                {
                    ("Authorization", p.EndpointAutorizacion),
                    ("Token", p.EndpointToken),
                    ("UserInfo", p.EndpointUserInfo),
                    ("JWKS", p.JwksUri),
                    ("Revocación", p.EndpointRevocacion)
                };
                foreach (var (campo, valor) in vals)
                {
                    if (!string.IsNullOrWhiteSpace(valor) && !valor.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        issues.Add(new CatalogValidationIssue(
                            "ENDPOINT_NO_HTTPS",
                            $"Endpoint {campo} de {p.Codigo} no es HTTPS: {valor}",
                            ESeveridadValidacion.Error));
                }
            }

            // 7-8. ResponseType y GrantType (desde ConfProvIden)
            var configsResult = await _confProvIdenRepo.ObtenerTodosAsync(ct);
            if (configsResult.IsFailure)
                return Result<IReadOnlyList<CatalogValidationIssue>>.Failure(configsResult.Error!);
            var configs = configsResult.Value;

            foreach (var c in configs)
            {
                if (!string.Equals(c.ResponseType, "code", StringComparison.OrdinalIgnoreCase))
                    issues.Add(new CatalogValidationIssue(
                        "RESPONSE_TYPE_INVALIDO",
                        $"ConfProvIden Id={c.Id} tiene ResponseType='{c.ResponseType}', se esperaba 'code'",
                        ESeveridadValidacion.Error));

                if (!string.Equals(c.GrantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
                    issues.Add(new CatalogValidationIssue(
                        "GRANT_TYPE_INVALIDO",
                        $"ConfProvIden Id={c.Id} tiene GrantType='{c.GrantType}', se esperaba 'authorization_code'",
                        ESeveridadValidacion.Error));
            }

            // 9. Issuer de Google
            var google = proveedores.FirstOrDefault(p => string.Equals(p.Codigo, "GOOGLE", StringComparison.OrdinalIgnoreCase));
            if (google is not null && !string.IsNullOrWhiteSpace(google.UrlIssuer) &&
                !string.Equals(google.UrlIssuer, "https://accounts.google.com", StringComparison.OrdinalIgnoreCase))
                issues.Add(new CatalogValidationIssue(
                    "ISSUER_INVALIDO",
                    $"Google Issuer esperado 'https://accounts.google.com', actual: {google.UrlIssuer}",
                    ESeveridadValidacion.Error));

            // 10. Consistencia de orden (secuencia continua 1,2,3...)
            var ordenes = proveedores.Select(p => p.Orden).OrderBy(o => o).ToList();
            for (int i = 0; i < ordenes.Count; i++)
            {
                if (ordenes[i] != i + 1)
                    issues.Add(new CatalogValidationIssue(
                        "ORDEN_INCONSISTENTE",
                        $"Secuencia de orden rota en posición {i + 1}: se esperaba {i + 1}, encontrado {ordenes[i]}",
                        ESeveridadValidacion.Warning));
            }

            // 11. Comprobar duplicados de orden
            var ordenDuplicados = proveedores.GroupBy(p => p.Orden).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            foreach (var ord in ordenDuplicados)
                issues.Add(new CatalogValidationIssue(
                    "ORDEN_DUPLICADO",
                    $"Varios proveedores tienen Orden = {ord}",
                    ESeveridadValidacion.Error));

            // 12. Icono obligatorio
            foreach (var p in proveedores)
            {
                if (string.IsNullOrWhiteSpace(p.Icono))
                    issues.Add(new CatalogValidationIssue(
                        "ICONO_REQUERIDO",
                        $"El proveedor {p.Codigo} no tiene Icono definido",
                        ESeveridadValidacion.Warning));
            }
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<CatalogValidationIssue>>.Failure("VALIDATION_ERROR", ex.Message);
        }

        return Result<IReadOnlyList<CatalogValidationIssue>>.Success(issues.AsReadOnly());
    }
}
