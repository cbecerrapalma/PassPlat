namespace PassPlat.Dominio.Enums;

// S123 · D1 — mismos números que la tabla dbo.EstIdenExt (la BD manda, decisión del dueño 2026-11-17)
public enum EEstIdenExt : byte
{
    Activo = 1,
    Inactivo = 2,
    Pendiente = 3,
    Revocado = 4,
    Expirado = 5,
    Bloqueado = 6,
    SinVerificar = 7
}
