namespace PassPlat.Client;

/// <summary>
/// Claims que PassPlat emite en sus JWT y que los consumidores usan para autorizar (PassPlat.Aplicacion:
/// <c>AuthenticationTokenIssuer</c>).
/// </summary>
public static class PassPlatClaims
{
    /// <summary>
    /// <c>is_system = "true"</c> solo para usuarios maestros (<c>Usuarios.EsSistema = 1</c>).
    /// Regla del dueño (S95, 2026-10-19): publicar o restaurar el catálogo de acceso de una app en PassPlat
    /// lo puede hacer SOLO un usuario maestro, desde cualquier consumidor; para el resto la operación no debe verse.
    /// </summary>
    public const string EsSistema = "is_system";

    /// <summary>Valor del claim <see cref="EsSistema"/> para un usuario maestro.</summary>
    public const string Verdadero = "true";
}
