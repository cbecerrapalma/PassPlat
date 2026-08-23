namespace PassPlat.Aplicacion.Dtos.Core;

public class DashboardDto
{
    public int TotalUsuarios { get; set; }
    public int UsuariosLocales { get; set; }
    public int UsuariosOAuth { get; set; }
    public int UsuariosHibridos { get; set; }
    public int UsuariosConMFA { get; set; }
    public int UsuariosBloqueados { get; set; }
    public int UsuariosInactivos { get; set; }
    public List<ProveedorConteoDto> Proveedores { get; set; } = [];
    public List<IntentoRecienteDto> IntentosRecientes { get; set; } = [];
}

public class ProveedorConteoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Vinculaciones { get; set; }
}

public class IntentoRecienteDto
{
    public long Id { get; set; }
    public string NomUsuario { get; set; } = string.Empty;
    public string MetodoAutenticacion { get; set; } = string.Empty;
    public bool Exitoso { get; set; }
    public DateTime FecIntento { get; set; }
}
