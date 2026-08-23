using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IEmailTemplateRepository : IRepositoryAsync<EmailTemplate>
{
    Task<Result<EmailTemplate?>> ObtenerPorNombreCulturaAsync(string nombre, string cultura, int? idTenant = null, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailTemplate>>> ObtenerPublicadosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailTemplate>>> ObtenerPorCategoriaAsync(string categoria, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailTemplate>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
}

public interface IEmailTemplatePartialRepository : IRepositoryAsync<EmailTemplatePartial>
{
    Task<Result<EmailTemplatePartial?>> ObtenerPorNombreAsync(string nombre, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailTemplatePartial>>> ObtenerActivosAsync(CancellationToken ct = default);
}

public interface IEmailTemplateHistorialRepository : IRepositoryAsync<EmailTemplateHistorial>
{
    Task<Result<IReadOnlyList<EmailTemplateHistorial>>> ObtenerPorTemplateAsync(int idTemplate, CancellationToken ct = default);
    Task<Result<EmailTemplateHistorial?>> ObtenerVersionAsync(int idTemplate, int version, CancellationToken ct = default);
}

public class EmailTemplateRepository : RepositoryAsync<EmailTemplate>, IEmailTemplateRepository
{
    public EmailTemplateRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<EmailTemplate?>> ObtenerPorNombreCulturaAsync(string nombre, string cultura, int? idTenant = null, CancellationToken ct = default)
    {
        try
        {
            const string DefaultCulture = "es";

            // 1. Exact match
            var entity = await DbSet.FirstOrDefaultAsync(e => e.Nombre == nombre && e.Cultura == cultura && e.IdTenant == idTenant, ct);
            if (entity != null)
                return Result<EmailTemplate?>.Success(entity);

            // 2. Fallback to global (idTenant = NULL) if tenant-specific was requested
            if (idTenant.HasValue)
            {
                entity = await DbSet.FirstOrDefaultAsync(e => e.Nombre == nombre && e.Cultura == cultura && e.IdTenant == null, ct);
                if (entity != null)
                    return Result<EmailTemplate?>.Success(entity);
            }

            // 3. Fallback to default culture if cultura != DefaultCulture
            if (!string.Equals(cultura, DefaultCulture, StringComparison.OrdinalIgnoreCase))
            {
                entity = await DbSet.FirstOrDefaultAsync(e => e.Nombre == nombre && e.Cultura == DefaultCulture && e.IdTenant == idTenant, ct);
                if (entity != null)
                    return Result<EmailTemplate?>.Success(entity);

                if (idTenant.HasValue)
                {
                    entity = await DbSet.FirstOrDefaultAsync(e => e.Nombre == nombre && e.Cultura == DefaultCulture && e.IdTenant == null, ct);
                    if (entity != null)
                        return Result<EmailTemplate?>.Success(entity);
                }
            }

            return Result<EmailTemplate?>.Success(null, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<EmailTemplate?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<EmailTemplate>>> ObtenerPublicadosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(e => e.Estado == "publicado").OrderBy(e => e.Nombre).ToListAsync(ct);
            return Result<IReadOnlyList<EmailTemplate>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<EmailTemplate>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<EmailTemplate>>> ObtenerPorCategoriaAsync(string categoria, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(e => e.Categoria == categoria).OrderBy(e => e.Nombre).ToListAsync(ct);
            return Result<IReadOnlyList<EmailTemplate>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<EmailTemplate>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<EmailTemplate>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(e => e.IdTenant == idTenant || e.IdTenant == null).OrderBy(e => e.Nombre).ToListAsync(ct);
            return Result<IReadOnlyList<EmailTemplate>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<EmailTemplate>>.Failure("DB_ERROR", ex.Message);
        }
    }
}

public class EmailTemplatePartialRepository : RepositoryAsync<EmailTemplatePartial>, IEmailTemplatePartialRepository
{
    public EmailTemplatePartialRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<EmailTemplatePartial?>> ObtenerPorNombreAsync(string nombre, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(e => e.Nombre == nombre, ct);
            return Result<EmailTemplatePartial?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<EmailTemplatePartial?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<EmailTemplatePartial>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(e => e.Activo).OrderBy(e => e.Nombre).ToListAsync(ct);
            return Result<IReadOnlyList<EmailTemplatePartial>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<EmailTemplatePartial>>.Failure("DB_ERROR", ex.Message);
        }
    }
}

public class EmailTemplateHistorialRepository : RepositoryAsync<EmailTemplateHistorial>, IEmailTemplateHistorialRepository
{
    public EmailTemplateHistorialRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<EmailTemplateHistorial>>> ObtenerPorTemplateAsync(int idTemplate, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(h => h.IdTemplate == idTemplate).OrderByDescending(h => h.Version).ToListAsync(ct);
            return Result<IReadOnlyList<EmailTemplateHistorial>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<EmailTemplateHistorial>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<EmailTemplateHistorial?>> ObtenerVersionAsync(int idTemplate, int version, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(h => h.IdTemplate == idTemplate && h.Version == version, ct);
            return Result<EmailTemplateHistorial?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<EmailTemplateHistorial?>.Failure("DB_ERROR", ex.Message);
        }
    }
}
