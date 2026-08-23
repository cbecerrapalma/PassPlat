namespace PassPlat.Aplicacion.Dtos.Core;

public class PoliticaPwdDto
{
    public int Id { get; set; }
    public int? IdTenant { get; set; }
    public int? IdApp { get; set; }
    public int Version { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public byte LongMin { get; set; }
    public byte LongMax { get; set; }
    public bool ReqMayuscula { get; set; }
    public bool ReqMinuscula { get; set; }
    public bool ReqNumero { get; set; }
    public bool ReqEspecial { get; set; }
    public string? CaracteresEspeciales { get; set; }
    public bool ProhSecuenciales { get; set; }
    public bool ProhRepetitivos { get; set; }
    public bool ProhPatrones { get; set; }
    public bool ProhPwdComun { get; set; }
    public bool ProhInfoUsuario { get; set; }
    public bool VerificarBrechas { get; set; }
    public bool PermitirEspacios { get; set; }
    public short DiasVigencia { get; set; }
    public byte PwdRecordadas { get; set; }
    public byte MaxIntentos { get; set; }
    public int DurBloqueoMin { get; set; }
    public bool Activa { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime FecMod { get; set; }
    public int? IdUsrMod { get; set; }
    public string? TenantNombre { get; set; }
    public string? AppNombre { get; set; }
}

public class CrearPoliticaPwdDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int? IdTenant { get; set; }
    public int? IdApp { get; set; }
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
}

public class ActualizarPoliticaPwdDto
{
    public string? Nombre { get; set; }
    public byte? LongMin { get; set; }
    public byte? LongMax { get; set; }
    public bool? ReqMayuscula { get; set; }
    public bool? ReqMinuscula { get; set; }
    public bool? ReqNumero { get; set; }
    public bool? ReqEspecial { get; set; }
    public string? CaracteresEspeciales { get; set; }
    public bool? ProhSecuenciales { get; set; }
    public bool? ProhRepetitivos { get; set; }
    public bool? ProhPatrones { get; set; }
    public bool? ProhPwdComun { get; set; }
    public bool? ProhInfoUsuario { get; set; }
    public bool? VerificarBrechas { get; set; }
    public bool? PermitirEspacios { get; set; }
    public short? DiasVigencia { get; set; }
    public byte? PwdRecordadas { get; set; }
    public byte? MaxIntentos { get; set; }
    public int? DurBloqueoMin { get; set; }
}
