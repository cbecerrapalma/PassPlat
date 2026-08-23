using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IUsuarioService : IServiceAsync<Usuario, UsuarioDto>
{
    Task<Result<UsuarioDto?>> ObtenerPorNomUsuarioAsync(int idTenant, string nomUsuario, CancellationToken ct = default);
    Task<Result<UsuarioDto?>> ObtenerPorEmailAsync(int idTenant, string email, CancellationToken ct = default);
    Task<Result<UsuarioDto?>> ObtenerCompletoPorIdAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<UsuarioDto>>> ObtenerConIntentosExcedidosAsync(int idTenant, byte maxIntentos, CancellationToken ct = default);
    Task<Result<IReadOnlyList<UsuarioDto>>> ObtenerConPasswordExpiradaAsync(int idTenant, int diasVigencia, CancellationToken ct = default);
    Task<Result<UsuarioDto>> CrearAsync(CrearUsuarioDto dto, CancellationToken ct = default);
    Task<Result> ActualizarAsync(ActualizarUsuarioDto dto, CancellationToken ct = default);
    Task<Result> MarcarEliminadoAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IPagedResult<UsuarioDto>>> ObtenerPaginadoConBusquedaAsync(PaginationOptions<Usuario> options, string search, CancellationToken ct = default);
    Task<Result<IPagedResult<UsuarioDto>>> ObtenerPaginadoPorTenantAsync(int idTenant, PaginationOptions<Usuario> options, string? search, CancellationToken ct = default);
    Task<Result<CrearUsuarioResult>> CrearConPasswordAsync(int idTenant, int idEstado, string nomUsuario, string? email, string nombre, string apellido, string? hashPwd, string algoritmo = "Argon2id", byte pepperVersion = 1, int? idTipoCambio = null, bool emailVerificado = false, int? idUsrEjecutor = null, int? idTipoAccion = null, int? idDisp = null, int? idIP = null, int? idAgente = null, CancellationToken ct = default);
    Task EnviarBienvenidaAsync(string? email, string nomUsuario, int? idTenant = null, int? idUsuario = null, int? idApp = null, CancellationToken ct = default);
}

public class UsuarioService : ServiceAsync<Usuario, UsuarioDto>, IUsuarioService
{
    private readonly UsuarioRepository _repo;
    private readonly IEmailQueue _emailQueue;
    private readonly IConfigAppRepository _configAppRepo;
    private readonly IUnitOfWorkAsync _uow;
    private readonly ILogger<UsuarioService> _logger;

    public UsuarioService(UsuarioRepository repo, IUnitOfWorkAsync uow, IMapper mapper, IEmailQueue emailQueue, IConfigAppRepository configAppRepo, ILogger<UsuarioService> logger)
        : base(repo, mapper)
    {
        _repo = repo;
        _emailQueue = emailQueue;
        _configAppRepo = configAppRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<UsuarioDto?>> ObtenerPorNomUsuarioAsync(int idTenant, string nomUsuario, CancellationToken ct = default)
    {
        var usuarioResult = await _repo.ObtenerPorNomUsuarioAsync(idTenant, nomUsuario, ct);
        if (usuarioResult.IsFailure) return Result<UsuarioDto?>.Failure(usuarioResult.Error!);
        var dto = usuarioResult.Value != null ? Mapper.Map<UsuarioDto>(usuarioResult.Value) : null;
        return Result<UsuarioDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<UsuarioDto?>> ObtenerPorEmailAsync(int idTenant, string email, CancellationToken ct = default)
    {
        var usuarioResult = await _repo.ObtenerPorEmailAsync(idTenant, email, ct);
        if (usuarioResult.IsFailure) return Result<UsuarioDto?>.Failure(usuarioResult.Error!);
        var dto = usuarioResult.Value != null ? Mapper.Map<UsuarioDto>(usuarioResult.Value) : null;
        return Result<UsuarioDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<UsuarioDto?>> ObtenerCompletoPorIdAsync(int idUsuario, CancellationToken ct = default)
    {
        var usuarioResult = await _repo.ObtenerCompletoPorIdAsync(idUsuario, ct);
        if (usuarioResult.IsFailure) return Result<UsuarioDto?>.Failure(usuarioResult.Error!);
        var dto = usuarioResult.Value != null ? Mapper.Map<UsuarioDto>(usuarioResult.Value) : null;
        return Result<UsuarioDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<IReadOnlyList<UsuarioDto>>> ObtenerConIntentosExcedidosAsync(int idTenant, byte maxIntentos, CancellationToken ct = default)
    {
        var usuariosResult = await _repo.ObtenerConIntentosExcedidosAsync(idTenant, maxIntentos, ct);
        if (usuariosResult.IsFailure) return Result<IReadOnlyList<UsuarioDto>>.Failure(usuariosResult.Error!);
        return Result<IReadOnlyList<UsuarioDto>>.Success(Mapper.Map<IReadOnlyList<UsuarioDto>>(usuariosResult.Value));
    }

    public async Task<Result<IReadOnlyList<UsuarioDto>>> ObtenerConPasswordExpiradaAsync(int idTenant, int diasVigencia, CancellationToken ct = default)
    {
        var usuariosResult = await _repo.ObtenerConPasswordExpiradaAsync(idTenant, diasVigencia, ct);
        if (usuariosResult.IsFailure) return Result<IReadOnlyList<UsuarioDto>>.Failure(usuariosResult.Error!);
        return Result<IReadOnlyList<UsuarioDto>>.Success(Mapper.Map<IReadOnlyList<UsuarioDto>>(usuariosResult.Value));
    }

    public async Task<Result<UsuarioDto>> CrearAsync(CrearUsuarioDto dto, CancellationToken ct = default)
    {
        var usuario = Usuario.Crear(dto.IdTenant, dto.IdEstado, dto.NomUsuario, dto.Email, dto.Nombre, dto.Apellido);
        var addResult = _repo.Add(usuario);
        if (addResult.IsFailure) return Result<UsuarioDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<UsuarioDto>.Success(Mapper.Map<UsuarioDto>(usuario));
    }

    public async Task<Result> ActualizarAsync(ActualizarUsuarioDto dto, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(dto.Id, ct);
        if (result.IsFailure) return Result.Failure("USUARIO_NO_ENCONTRADO", "Usuario no encontrado");
        var usuario = result.Value;

        var estadoAnterior = usuario.IdEstado;

        if (dto.IdEstado.HasValue) usuario.IdEstado = dto.IdEstado.Value;
        if (dto.Nombre != null) usuario.Nombre = dto.Nombre;
        if (dto.Apellido != null) usuario.Apellido = dto.Apellido;
        if (dto.Email != null) usuario.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        if (dto.EmailVerificado.HasValue) usuario.EmailVerificado = dto.EmailVerificado.Value;

        var updateResult = _repo.Update(usuario);
        if (updateResult.IsFailure) return Result.Failure(updateResult.Error!);

        if (dto.IdEstado.HasValue && dto.IdEstado.Value != estadoAnterior && !string.IsNullOrWhiteSpace(usuario.Email))
        {
            var templateCode = dto.IdEstado.Value switch
            {
                1 => "user-activated",
                2 => "user-deactivated",
                _ => (string?)null
            };
            if (templateCode != null)
                await NotificarEventoAsync(templateCode, usuario.Email, usuario.NomUsuario, usuario.IdTenant, usuario.Id, null, ct);
        }

        return Result.Success();
    }

    public async Task<Result> MarcarEliminadoAsync(int idUsuario, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(idUsuario, ct);
        if (result.IsFailure) return Result.Failure("USUARIO_NO_ENCONTRADO", "Usuario no encontrado");
        var usuario = result.Value;

        if (usuario.Eliminado) return Result.Failure("USUARIO_YA_ELIMINADO", "Usuario ya está eliminado");

        usuario.MarcarEliminado();
        var updResult = _repo.Update(usuario);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        return Result.Success();
    }

    public async Task<Result<CrearUsuarioResult>> CrearConPasswordAsync(int idTenant, int idEstado, string nomUsuario, string? email, string nombre, string apellido, string? hashPwd, string algoritmo = "Argon2id", byte pepperVersion = 1, int? idTipoCambio = null, bool emailVerificado = false, int? idUsrEjecutor = null, int? idTipoAccion = null, int? idDisp = null, int? idIP = null, int? idAgente = null, CancellationToken ct = default)
    {
        return await _repo.CrearConPasswordAsync(idTenant, idEstado, nomUsuario, email, nombre, apellido, hashPwd, algoritmo, pepperVersion, idTipoCambio, emailVerificado, idUsrEjecutor, idTipoAccion, idDisp, idIP, idAgente, ct);
    }

    public async Task EnviarBienvenidaAsync(string? email, string nomUsuario, int? idTenant = null, int? idUsuario = null, int? idApp = null, CancellationToken ct = default)
    {
        await NotificarBienvenidaAsync(email, nomUsuario, idTenant, idUsuario, idApp, ct);
    }

    private async Task NotificarBienvenidaAsync(string? email, string nomUsuario, int? idTenant, int? idUsuario, int? idApp, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return;

            var configResult = await _configAppRepo.ObtenerPorGrupoAsync("General", ct);
            var configs = configResult.IsSuccess ? configResult.Value : null;
            var dict = configs?.Where(c => c.Activo).ToDictionary(c => c.Clave, c => c.Valor, StringComparer.OrdinalIgnoreCase);
            var appName = dict?.GetValueOrDefault("App_Titulo") ?? "PassPlat";
            var loginUrl = dict?.GetValueOrDefault("App_UrlBase") ?? "https://localhost:7275";

            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.Welcome,
                email,
                nomUsuario,
                new Dictionary<string, object?>
                {
                    ["AppName"] = appName,
                    ["LoginUrl"] = loginUrl
                },
                idTenant,
                idUsuario,
                idApp,
                null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar email de bienvenida a {Email}", email);
        }
    }

    public async Task<Result<IPagedResult<UsuarioDto>>> ObtenerPaginadoConBusquedaAsync(PaginationOptions<Usuario> options, string search, CancellationToken ct = default)
    {
        var pagResult = await _repo.BuscarPaginadoAsync(search, options.PageNumber, options.PageSize, ct);
        if (pagResult.IsFailure) return Result<IPagedResult<UsuarioDto>>.Failure(pagResult.Error!);
        var (items, totalCount) = pagResult.Value;
        var mapped = Mapper.Map<IReadOnlyList<UsuarioDto>>(items);
        return Result<IPagedResult<UsuarioDto>>.Success(
            new PagedResultDto<UsuarioDto>(mapped, totalCount, options.PageNumber, options.PageSize));
    }

    public async Task<Result<IPagedResult<UsuarioDto>>> ObtenerPaginadoPorTenantAsync(int idTenant, PaginationOptions<Usuario> options, string? search, CancellationToken ct = default)
    {
        var pagResult = await _repo.BuscarPaginadoPorTenantAsync(idTenant, search ?? "", options.PageNumber, options.PageSize, ct);
        if (pagResult.IsFailure) return Result<IPagedResult<UsuarioDto>>.Failure(pagResult.Error!);
        var (items, totalCount) = pagResult.Value;
        var mapped = Mapper.Map<IReadOnlyList<UsuarioDto>>(items);
        return Result<IPagedResult<UsuarioDto>>.Success(
            new PagedResultDto<UsuarioDto>(mapped, totalCount, options.PageNumber, options.PageSize));
    }

    private async Task NotificarEventoAsync(string templateCode, string email, string nomUsuario, int? idTenant, int? idUsuario, int? idApp, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            var kind = templateCode switch
            {
                "user-activated" => EmailJobKind.UserActivated,
                "user-deactivated" => EmailJobKind.UserDeactivated,
                _ => EmailJobKind.UserActivated
            };
            await _emailQueue.EnqueueAsync(new EmailJob(
                kind,
                email,
                nomUsuario,
                new Dictionary<string, object?>(),
                idTenant,
                idUsuario,
                idApp ?? 1,
                null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar notificación {TemplateCode} para {Email}", templateCode, email);
        }
    }
}
