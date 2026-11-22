namespace PassPlat.Aplicacion.Dtos.Core;

// S77 · D2 — contrato del inicio por perfil: una sola peticion alimenta las tres vistas.
// Un valor null significa que el actor no tiene el permiso: no se calcula ni se envia.
public class InicioDto
{
    public InicioCuentaDto Cuenta { get; set; } = new();
    public InicioTenantDto? Tenant { get; set; }
    public InicioPlataformaDto? Plataforma { get; set; }
}

public class InicioCuentaDto
{
    public DateTime? FecExpiraPwd { get; set; }
    public int MetodosMfaActivos { get; set; }
    public int SesionesActivas { get; set; }
    public int DispositivosConfiables { get; set; }
}

public class InicioTenantDto
{
    public int? Usuarios { get; set; }
    public int? SesionesActivas { get; set; }
    public int? ConIntentosExcedidos { get; set; }
    public int? ConPasswordExpirada { get; set; }
    public int? DispConfiables { get; set; }
    public int? DispNoConfiables { get; set; }
    public IReadOnlyList<AuditoriaPwdDto>? Actividad { get; set; }
}

public class InicioPlataformaDto
{
    public int? TenantsActivos { get; set; }
    public int? Apps { get; set; }
    public int? Usuarios { get; set; }
    public IReadOnlyList<BackgroundJobDto>? Servicios { get; set; }
}
