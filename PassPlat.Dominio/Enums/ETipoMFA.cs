namespace PassPlat.Dominio.Enums;

public enum ETipoMFA : byte
{
    TOTP = 1,
    SMS = 2,
    Email = 3,
    WebAuthn = 4,
    Push = 5,
    BackupCodes = 6
}
