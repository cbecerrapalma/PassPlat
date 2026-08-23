using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Contexto;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public record IPRegistro(IP Entidad, bool EsNueva);

public interface IIPRepository : IRepositoryAsync<IP>
{
    Task<Result<IP?>> ObtenerPorDireccionAsync(string direccion, CancellationToken ct = default);
    Result<IPRegistro> ObtenerOCrear(string direccion, byte tipoIP = 0, string? pais = null, string? ciudad = null);
    Result MarcarComoSospechosa(int idIP);
}

public class IPRepository : RepositoryAsync<IP>, IIPRepository
{
    public IPRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IP?>> ObtenerPorDireccionAsync(string direccion, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(d => d.Direccion == direccion, ct);
            return Result<IP?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<IP?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result<IPRegistro> ObtenerOCrear(string direccion, byte tipoIP = 0, string? pais = null, string? ciudad = null)
    {
        try
        {
            var ip = DbSet.FirstOrDefault(d => d.Direccion == direccion);
            if (ip != null)
            {
                ip.UltUso = DateTime.Now;
                return Result<IPRegistro>.Success(new IPRegistro(ip, false));
            }
            ip = IP.Crear(direccion, tipoIP, pais, ciudad);
            ip.UltUso = DateTime.Now;
            DbSet.Add(ip);
            return Result<IPRegistro>.Success(new IPRegistro(ip, true));
        }
        catch (Exception ex)
        {
            return Result<IPRegistro>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result MarcarComoSospechosa(int idIP)
    {
        try
        {
            var ip = DbSet.FirstOrDefault(d => d.Id == idIP && !d.EsSospechosa);
            if (ip == null)
                return Result.Failure("IP_NOT_FOUND", "IP no encontrada o ya marcada como sospechosa");

            ip.EsSospechosa = true;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }
}
