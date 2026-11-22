using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

// S102 · D4 — alcanceTenant: null = is_system; con valor = solo permisos directos de ese tenant.
// S103 — editor unificado por (tenant, app): contextos, editor y aplicar en una petición.
public interface IUsuarioPermisoService : IServiceAsync<UsuarioPermiso, UsuarioPermisoDto>
{
    Task<Result<IReadOnlyList<UsuarioPermisoDto>>> ObtenerPorUsuarioAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ContextoPermisoDto>>> ObtenerContextosAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<EditorPermisosDto>> ObtenerEditorAsync(int idUsuario, int idTenant, int idApp, CancellationToken ct = default);
    // S110 · D2 — método único: apps permitidas por usuario y tenant
    Task<Result<IReadOnlyList<int>?>> ObtenerAppsPermitidasAsync(int idUsuario, int? alcanceTenant, int? idApp, CancellationToken ct = default);
    // S107 · D1 — AUTOEDICION: nadie cambia ni revoca sus propios permisos, salvo el usuario de sistema
    Task<Result<ResultadoAplicarPermisosDto>> AplicarPermisosAsync(AplicarPermisosUsuarioDto dto, int idUsrEjecutor, bool ejecutorEsSistema, CancellationToken ct = default);
    Task<Result> RevocarPermisoAsync(int id, int? alcanceTenant, int idUsrEjecutor, CancellationToken ct = default);
}

public class UsuarioPermisoService : ServiceAsync<UsuarioPermiso, UsuarioPermisoDto>, IUsuarioPermisoService
{
    private const byte Conceder = 1;
    private const byte Denegar = 2;
    private const int MaxCambios = 500;

    private readonly IUsuarioPermisoRepository _repo;
    private readonly IAccesoRepository _accesos;
    private readonly IUsuarioTenantRepository _usuarioTenants;
    private readonly IPermisoRepository _permisos;
    private readonly IAuthRepository _auth;

    public UsuarioPermisoService(IUsuarioPermisoRepository repo, IAccesoRepository accesos, IUsuarioTenantRepository usuarioTenants,
        IPermisoRepository permisos, IAuthRepository auth, IMapper mapper)
        : base(repo, mapper)
    {
        _repo = repo;
        _accesos = accesos;
        _usuarioTenants = usuarioTenants;
        _permisos = permisos;
        _auth = auth;
    }

    private static Error Validacion(string codigo, string mensaje) => new(codigo, mensaje, null, 400, ErrorType.Validation);

    // vigente: FecInicio <= ahora < FecFin, con nulos abiertos (misma regla que SP_Permisos_Usuario_Efectivos)
    private static bool Vigente(UsuarioPermiso fila, DateTime ahora) =>
        (fila.FecInicio is null || fila.FecInicio <= ahora) && (fila.FecFin is null || ahora < fila.FecFin);

    public async Task<Result<IReadOnlyList<UsuarioPermisoDto>>> ObtenerPorUsuarioAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (result.IsFailure) return Result<IReadOnlyList<UsuarioPermisoDto>>.Failure(result.Error!);
        IReadOnlyList<UsuarioPermiso> lista = result.Value;
        if (alcanceTenant.HasValue)
            lista = [.. lista.Where(up => up.IdTenant == alcanceTenant.Value)];
        return Result<IReadOnlyList<UsuarioPermisoDto>>.Success(Mapper.Map<IReadOnlyList<UsuarioPermisoDto>>(lista));
    }

    // D1 — apps donde el usuario tiene un Acceso activo, app activa y membresía en ese tenant, dentro del alcance
    public async Task<Result<IReadOnlyList<ContextoPermisoDto>>> ObtenerContextosAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default)
    {
        var accesos = await _accesos.ObtenerAccesosUsuarioAsync(idUsuario, ct);
        if (accesos.IsFailure) return Result<IReadOnlyList<ContextoPermisoDto>>.Failure(accesos.Error!);
        var membresias = await _usuarioTenants.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (membresias.IsFailure) return Result<IReadOnlyList<ContextoPermisoDto>>.Failure(membresias.Error!);

        var tenantsConMembresia = membresias.Value.Where(ut => ut.Activo).Select(ut => ut.IdTenant).ToHashSet();

        IReadOnlyList<ContextoPermisoDto> contextos =
        [
            .. accesos.Value
                .Where(a => a.Activo && a.App is { Activa: true } && tenantsConMembresia.Contains(a.IdTenant)
                            && (alcanceTenant is null || alcanceTenant == a.IdTenant))
                .GroupBy(a => (a.IdTenant, a.IdApp))
                .Select(g => new ContextoPermisoDto
                {
                    IdTenant = g.Key.IdTenant,
                    TenantNombre = g.First().Tenant?.Nombre ?? string.Empty,
                    IdApp = g.Key.IdApp,
                    AppNombre = g.First().App!.Nombre,
                    AppCodigo = g.First().App!.Codigo,
                    Roles = [.. g.Where(a => a.Rol is { Activo: true })
                        .Select(a => new RolContextoDto { IdRol = a.IdRol, Nombre = a.Rol!.Nombre })
                        .DistinctBy(r => r.IdRol)
                        .OrderBy(r => r.Nombre)]
                })
                .OrderBy(c => c.TenantNombre, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(c => c.AppNombre, StringComparer.CurrentCultureIgnoreCase)
        ];
        return Result<IReadOnlyList<ContextoPermisoDto>>.Success(contextos);
    }

    private async Task<Result<ContextoPermisoDto>> ValidarContextoAsync(int idUsuario, int idTenant, int idApp, CancellationToken ct)
    {
        var contextos = await ObtenerContextosAsync(idUsuario, null, ct);
        if (contextos.IsFailure) return Result<ContextoPermisoDto>.Failure(contextos.Error!);
        var contexto = contextos.Value.FirstOrDefault(c => c.IdTenant == idTenant && c.IdApp == idApp);
        return contexto is null
            ? Result<ContextoPermisoDto>.Failure(Validacion("SIN_ACCESO_APP", "El usuario no tiene acceso activo a esa aplicación en el tenant"))
            : Result<ContextoPermisoDto>.Success(contexto);
    }

    // D2 — apps donde el usuario tiene acceso, filtrando por idApp si se proporciona
    public async Task<Result<IReadOnlyList<int>?>> ObtenerAppsPermitidasAsync(int idUsuario, int? alcanceTenant, int? idApp, CancellationToken ct = default)
    {
        if (alcanceTenant is null)
        {
            // Escenario sistema: sin validar, devolver null (todas) o [idApp] si se especifica
            if (idApp.HasValue)
                return Result<IReadOnlyList<int>?>.Success(new List<int> { idApp.Value });
            return Result<IReadOnlyList<int>?>.Success(null, allowNull: true);
        }

        // Con alcance de tenant: validar contra ObtenerContextosAsync
        var contextos = await ObtenerContextosAsync(idUsuario, alcanceTenant, ct);
        if (contextos.IsFailure) return Result<IReadOnlyList<int>?>.Failure(contextos.Error!);

        var idsAppConAcceso = contextos.Value.Select(c => c.IdApp).Distinct().ToHashSet();

        if (idApp.HasValue)
        {
            // Si se especifica idApp, verificar que el usuario tenga acceso a esa app
            if (idsAppConAcceso.Contains(idApp.Value))
                return Result<IReadOnlyList<int>?>.Success(new List<int> { idApp.Value });
            // Sin acceso → devolver 403 Forbidden
            return Result<IReadOnlyList<int>?>.Failure(new Error("SIN_ACCESO_APP", "El usuario no tiene acceso activo a esa aplicación en el tenant", null, 403, ErrorType.Forbidden));
        }

        // Sin idApp: devolver todas las apps con acceso (puede ser lista vacía, nunca null)
        return Result<IReadOnlyList<int>?>.Success([.. idsAppConAcceso.Order()]);
    }

    // D3 — todos los permisos de la app con su origen (rol, directo) y si el usuario los tiene
    public async Task<Result<EditorPermisosDto>> ObtenerEditorAsync(int idUsuario, int idTenant, int idApp, CancellationToken ct = default)
    {
        var contexto = await ValidarContextoAsync(idUsuario, idTenant, idApp, ct);
        if (contexto.IsFailure) return Result<EditorPermisosDto>.Failure(contexto.Error!);

        var usuario = await _auth.ObtenerUsuarioBasicoAsync(idUsuario, ct);
        if (usuario.IsFailure) return Result<EditorPermisosDto>.Failure(usuario.Error!);
        if (usuario.Value is null) return Result<EditorPermisosDto>.Failure(CommonErrors.NotFound.Entity("Usuario", idUsuario));

        var permisos = await _permisos.ObtenerActivosPorAppAsync(idApp, ct);
        if (permisos.IsFailure) return Result<EditorPermisosDto>.Failure(permisos.Error!);
        var porRol = await _auth.ObtenerPermisosDeRolesAsync(idUsuario, idTenant, idApp, ct);
        if (porRol.IsFailure) return Result<EditorPermisosDto>.Failure(porRol.Error!);
        var directos = await _repo.WhereAsync(up => up.IdUsuario == idUsuario && up.IdTenant == idTenant && up.Activo
                                                    && (up.IdApp == null || up.IdApp == idApp), asNoTracking: true, ct);
        if (directos.IsFailure) return Result<EditorPermisosDto>.Failure(directos.Error!);

        var ahora = DateTime.Now;
        var esSistema = usuario.Value.EsSistema;
        var rolesPorPermiso = porRol.Value.GroupBy(r => r.IdPermiso).ToDictionary(g => g.Key, g => g.ToList());
        var directosPorPermiso = directos.Value.GroupBy(d => d.IdPermiso).ToDictionary(g => g.Key, g => g.ToList());

        var filas = new List<PermisoEditorDto>(permisos.Value.Count);
        foreach (var permiso in permisos.Value)
        {
            rolesPorPermiso.TryGetValue(permiso.Id, out var roles);
            directosPorPermiso.TryGetValue(permiso.Id, out var filasDirectas);
            var directo = filasDirectas?.OrderByDescending(d => d.IdApp == idApp).ThenBy(d => d.Id).FirstOrDefault();
            var concede = filasDirectas?.Any(d => d.IdTipoAsig == Conceder && Vigente(d, ahora)) ?? false;
            var deniega = filasDirectas?.Any(d => d.IdTipoAsig == Denegar && Vigente(d, ahora)) ?? false;
            var porRolPermiso = roles is { Count: > 0 };

            filas.Add(new PermisoEditorDto
            {
                IdPermiso = permiso.Id,
                Codigo = permiso.Codigo,
                Nombre = permiso.Nombre,
                Descripcion = permiso.Descripcion,
                IdModulo = permiso.IdModulo,
                ModuloNombre = permiso.Modulo?.Nombre ?? string.Empty,
                Orden = permiso.Orden,
                PorRol = porRolPermiso,
                RolOrigen = porRolPermiso
                    ? string.Join(", ", roles!.Select(r => r.Heredado ? $"{r.RolNombre} (heredado)" : r.RolNombre).Distinct())
                    : null,
                IdTipoAsigDirecto = directo?.IdTipoAsig,
                FecInicio = directo?.FecInicio,
                FecFin = directo?.FecFin,
                Motivo = directo?.Motivo,
                DirectoVigente = directo is not null && Vigente(directo, ahora),
                Tiene = esSistema || ((porRolPermiso || concede) && !deniega)
            });
        }

        return Result<EditorPermisosDto>.Success(new EditorPermisosDto
        {
            IdTenant = idTenant,
            TenantNombre = contexto.Value.TenantNombre,
            IdApp = idApp,
            AppNombre = contexto.Value.AppNombre,
            EsSistema = esSistema,
            Roles = contexto.Value.Roles,
            Permisos = filas
        });
    }

    // D4 — todo o nada: valida, calcula en memoria qué fila crear, cambiar o desactivar y deja el commit al controlador
    public async Task<Result<ResultadoAplicarPermisosDto>> AplicarPermisosAsync(AplicarPermisosUsuarioDto dto, int idUsrEjecutor, bool ejecutorEsSistema, CancellationToken ct = default)
    {
        // S107 · D1 — nadie cambia sus propios permisos, salvo el usuario de sistema
        if (dto.IdUsuario == idUsrEjecutor && !ejecutorEsSistema)
            return Result<ResultadoAplicarPermisosDto>.Failure(Validacion("AUTOEDICION", "No puede cambiar sus propios permisos"));

        var cambios = dto.Cambios ?? [];
        if (cambios.Count is < 1 or > MaxCambios || cambios.Select(c => c.IdPermiso).Distinct().Count() != cambios.Count)
            return Result<ResultadoAplicarPermisosDto>.Failure(Validacion("CAMBIOS_INVALIDOS", $"Indique de 1 a {MaxCambios} permisos distintos"));
        if (dto.FecInicio.HasValue && dto.FecFin.HasValue && dto.FecFin <= dto.FecInicio)
            return Result<ResultadoAplicarPermisosDto>.Failure(Validacion("VIGENCIA_INVALIDA", "La fecha fin debe ser posterior a la fecha inicio"));

        var contexto = await ValidarContextoAsync(dto.IdUsuario, dto.IdTenant, dto.IdApp, ct);
        if (contexto.IsFailure) return Result<ResultadoAplicarPermisosDto>.Failure(contexto.Error!);

        var usuario = await _auth.ObtenerUsuarioBasicoAsync(dto.IdUsuario, ct);
        if (usuario.IsFailure) return Result<ResultadoAplicarPermisosDto>.Failure(usuario.Error!);
        if (usuario.Value is null) return Result<ResultadoAplicarPermisosDto>.Failure(CommonErrors.NotFound.Entity("Usuario", dto.IdUsuario));
        if (usuario.Value.EsSistema)
            return Result<ResultadoAplicarPermisosDto>.Failure(Validacion("USUARIO_SISTEMA", "Un usuario de sistema ya tiene todos los permisos"));

        var permisosApp = await _permisos.ObtenerActivosPorAppAsync(dto.IdApp, ct);
        if (permisosApp.IsFailure) return Result<ResultadoAplicarPermisosDto>.Failure(permisosApp.Error!);
        var idsApp = permisosApp.Value.Select(p => p.Id).ToHashSet();
        if (cambios.Any(c => !idsApp.Contains(c.IdPermiso)))
            return Result<ResultadoAplicarPermisosDto>.Failure(Validacion("PERMISO_FUERA_DE_APP", "Hay permisos que no pertenecen a la aplicación indicada"));

        var porRol = await _auth.ObtenerPermisosDeRolesAsync(dto.IdUsuario, dto.IdTenant, dto.IdApp, ct);
        if (porRol.IsFailure) return Result<ResultadoAplicarPermisosDto>.Failure(porRol.Error!);
        var conRol = porRol.Value.Select(r => r.IdPermiso).ToHashSet();

        var ids = cambios.Select(c => c.IdPermiso).ToList();
        var activas = await _repo.WhereAsync(up => up.IdUsuario == dto.IdUsuario && up.IdTenant == dto.IdTenant && up.Activo
                                                   && ids.Contains(up.IdPermiso) && (up.IdApp == null || up.IdApp == dto.IdApp),
            asNoTracking: false, ct);
        if (activas.IsFailure) return Result<ResultadoAplicarPermisosDto>.Failure(activas.Error!);
        var filasPorPermiso = activas.Value.GroupBy(f => f.IdPermiso).ToDictionary(g => g.Key, g => g.ToList());

        var ahora = DateTime.Now;
        var resultado = new ResultadoAplicarPermisosDto();
        var nuevas = new List<UsuarioPermiso>();
        var modificadas = new List<UsuarioPermiso>();

        void Cambiar(UsuarioPermiso fila, byte tipo)
        {
            fila.IdTipoAsig = tipo;
            fila.Motivo = dto.Motivo;
            fila.FecInicio = dto.FecInicio;
            fila.FecFin = dto.FecFin;
            Tocar(fila);
        }

        void Desactivar(UsuarioPermiso fila)
        {
            fila.Activo = false;
            Tocar(fila);
            resultado.Revocados++;
        }

        void Tocar(UsuarioPermiso fila)
        {
            fila.IdUsrMod = idUsrEjecutor;
            fila.FecMod = ahora;
            if (!modificadas.Contains(fila)) modificadas.Add(fila);
        }

        void Crear(int idPermiso, byte tipo)
        {
            var fila = UsuarioPermiso.Crear(dto.IdUsuario, idPermiso, dto.IdTenant, tipo, idUsrEjecutor, dto.IdApp, dto.Motivo);
            fila.FecInicio = dto.FecInicio;
            fila.FecFin = dto.FecFin;
            nuevas.Add(fila);
        }

        foreach (var cambio in cambios)
        {
            filasPorPermiso.TryGetValue(cambio.IdPermiso, out var filas);
            // X: la fila directa activa del permiso (la de la app antes que la global)
            var x = filas?.OrderByDescending(f => f.IdApp == dto.IdApp).ThenBy(f => f.Id).FirstOrDefault();
            var r = conRol.Contains(cambio.IdPermiso);

            if (cambio.Tiene)
            {
                if (x is null)
                {
                    if (r) { resultado.SinCambio++; continue; }
                    Crear(cambio.IdPermiso, Conceder);
                    resultado.Concedidos++;
                }
                else if (x.IdTipoAsig == Denegar && r) Desactivar(x);
                else if (x.IdTipoAsig == Denegar) { Cambiar(x, Conceder); resultado.Concedidos++; }
                else { Cambiar(x, Conceder); resultado.Concedidos++; }
            }
            else
            {
                if (x is null)
                {
                    if (!r) { resultado.SinCambio++; continue; }
                    Crear(cambio.IdPermiso, Denegar);
                    resultado.Denegados++;
                }
                else if (x.IdTipoAsig == Conceder && r) { Cambiar(x, Denegar); resultado.Denegados++; }
                else if (x.IdTipoAsig == Conceder) Desactivar(x);
                else { Cambiar(x, Denegar); resultado.Denegados++; }
            }

            // otras filas activas del mismo permiso (la global y la de la app a la vez) que contradigan el pedido
            foreach (var otra in filas?.Where(f => !ReferenceEquals(f, x) && f.IdTipoAsig == (cambio.Tiene ? Denegar : Conceder)) ?? [])
                Desactivar(otra);
        }

        if (nuevas.Count > 0)
        {
            var add = _repo.AddRange(nuevas);
            if (add.IsFailure) return Result<ResultadoAplicarPermisosDto>.Failure(add.Error!);
        }
        if (modificadas.Count > 0)
        {
            var upd = _repo.UpdateRange(modificadas);
            if (upd.IsFailure) return Result<ResultadoAplicarPermisosDto>.Failure(upd.Error!);
        }
        return Result<ResultadoAplicarPermisosDto>.Success(resultado);
    }

    public async Task<Result> RevocarPermisoAsync(int id, int? alcanceTenant, int idUsrEjecutor, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure) return Result.Failure(result.Error!);
        if (alcanceTenant.HasValue && result.Value.IdTenant != alcanceTenant.Value)
            return Result.Failure(CommonErrors.NotFound.Entity("UsuarioPermiso", id));
        // S107 · D1 — tampoco revoca sus propias filas (quitar un «denegado» también da permisos); alcance null = sistema
        if (alcanceTenant.HasValue && result.Value.IdUsuario == idUsrEjecutor)
            return Result.Failure(Validacion("AUTOEDICION", "No puede cambiar sus propios permisos"));

        result.Value.Activo = false;
        result.Value.FecMod = DateTime.Now;
        var updResult = Repository.Update(result.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        return Result.Success();
    }
}
