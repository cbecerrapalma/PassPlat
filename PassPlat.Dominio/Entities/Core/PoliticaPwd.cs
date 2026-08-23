namespace PassPlat.Dominio.Entities.Core;

public class PoliticaPwd
{
    public int Id { get; set; }
    public int? IdTenant { get; set; }
    public int? IdApp { get; set; }
    public int Version { get; set; } = 1;
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public byte LongMin { get; set; } = 8;
    public byte LongMax { get; set; } = 64;
    public bool ReqMayuscula { get; set; }
    public bool ReqMinuscula { get; set; }
    public bool ReqNumero { get; set; }
    public bool ReqEspecial { get; set; }
    public string? CaracteresEspeciales { get; set; }
    public bool ProhSecuenciales { get; set; }
    public bool ProhRepetitivos { get; set; }
    public bool ProhPatrones { get; set; }
    public bool ProhPwdComun { get; set; } = true;
    public bool ProhInfoUsuario { get; set; } = true;
    public bool VerificarBrechas { get; set; } = true;
    public bool PermitirEspacios { get; set; } = true;
    public short DiasVigencia { get; set; }
    public byte PwdRecordadas { get; set; } = 24;
    public byte MaxIntentos { get; set; } = 5;
    public int DurBloqueoMin { get; set; } = 30;
    public bool Activa { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;
    public DateTime FecMod { get; set; } = DateTime.Now;
    public int? IdUsrMod { get; set; }

    public Tenant? Tenant { get; set; }
    public App? App { get; set; }
    public Usuario? UsrMod { get; set; }
    public ICollection<HistorialPwd> HistorialPwd { get; set; } = [];
    public ICollection<RolPoliticaPwd> RolesPoliticasPwd { get; set; } = [];

    public static PoliticaPwd Crear(string codigo, string nombre, byte longMin = 8, byte longMax = 64)
    {
        return new PoliticaPwd
        {
            Codigo = codigo,
            Nombre = nombre,
            LongMin = longMin,
            LongMax = longMax,
            ProhPwdComun = true,
            ProhInfoUsuario = true,
            VerificarBrechas = true,
            PermitirEspacios = true,
            Activa = true,
            Version = 1,
            FecCrea = DateTime.Now,
            FecMod = DateTime.Now
        };
    }

    public void Desactivar()
    {
        Activa = false;
        FecMod = DateTime.Now;
    }
}
