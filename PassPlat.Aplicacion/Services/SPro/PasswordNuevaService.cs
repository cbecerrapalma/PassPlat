using CBP.Results;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Services;

public interface IPasswordNuevaService
{
    Task<Result> ValidarAsync(int idUsuario, int idTenant, int? idApp, string passwordNueva, CancellationToken ct = default);
}

public class PasswordNuevaService : IPasswordNuevaService
{
    private readonly IPoliticaPwdService _politicas;
    private readonly IUsuarioRepository _usuarios;
    private readonly IHistorialPwdRepository _historial;
    private readonly IPassPlatPasswordSecurity _seguridad;
    private readonly CBP.Security.Cryptography.Services.IPasswordService _hashing;

    public PasswordNuevaService(
        IPoliticaPwdService politicas,
        IUsuarioRepository usuarios,
        IHistorialPwdRepository historial,
        IPassPlatPasswordSecurity seguridad,
        CBP.Security.Cryptography.Services.IPasswordService hashing)
    {
        _politicas = politicas;
        _usuarios = usuarios;
        _historial = historial;
        _seguridad = seguridad;
        _hashing = hashing;
    }

    public async Task<Result> ValidarAsync(int idUsuario, int idTenant, int? idApp, string passwordNueva, CancellationToken ct = default)
    {
        var politicaResult = await _politicas.ObtenerPoliticaAplicableAsync(idTenant, idApp, ct);
        if (politicaResult.IsFailure) return Result.Failure(politicaResult.Error!);
        var politica = politicaResult.Value;
        if (politica == null)
            return Result.Failure("POLITICA_NOT_FOUND", "No hay política de contraseñas activa para el tenant");

        var usuarioResult = await _usuarios.ObtenerPorIdAsync(idUsuario, ct);
        if (usuarioResult.IsFailure) return Result.Failure(usuarioResult.Error!);
        var usuario = usuarioResult.Value;
        if (usuario == null)
            return Result.Failure("USUARIO_NO_ENCONTRADO", "Usuario no encontrado");

        var contexto = new CBP.Security.Cryptography.Models.ValidationContext
        {
            UserName = usuario.NomUsuario,
            Email = usuario.Email
        };
        var validacion = await _seguridad.ValidatePasswordAsync(passwordNueva, ACbp(politica), contexto, ct);
        if (!validacion.IsValid)
            return Result.Failure("PASSWORD_WEAK", string.Join("; ", validacion.ErrorMessages));

        if (politica.PwdRecordadas > 0)
        {
            var historialResult = await _historial.ObtenerHistorialRecienteAsync(idUsuario, politica.PwdRecordadas, ct);
            if (historialResult.IsFailure) return Result.Failure(historialResult.Error!);

            foreach (var fila in historialResult.Value)
            {
                if (await _hashing.VerifyAsync(fila.HashPwd, passwordNueva, null, ct))
                    return Result.Failure("PASSWORD_REUSE", "No puedes reutilizar una contraseña reciente");
            }
        }

        return Result.Success();
    }

    private static CBP.Security.Cryptography.Models.PoliticaPwd ACbp(PoliticaPwdDto p) => new()
    {
        LongMin = p.LongMin,
        LongMax = p.LongMax,
        ReqMayuscula = p.ReqMayuscula,
        ReqMinuscula = p.ReqMinuscula,
        ReqNumero = p.ReqNumero,
        ReqEspecial = p.ReqEspecial,
        CaracteresEspeciales = p.CaracteresEspeciales,
        ProhSecuenciales = p.ProhSecuenciales,
        ProhRepetitivos = p.ProhRepetitivos,
        ProhPatrones = p.ProhPatrones,
        ProhPwdComun = p.ProhPwdComun,
        ProhInfoUsuario = p.ProhInfoUsuario,
        VerificarBrechas = p.VerificarBrechas,
        PermitirEspacios = p.PermitirEspacios,
        PwdRecordadas = p.PwdRecordadas
    };
}
