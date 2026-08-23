using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Dominio.Entities.Core;

public class IdenExtTokens
{
    public long Id { get; set; }
    public long IdIdenExt { get; set; }

    public byte[]? AccessTokenEnc { get; set; }
    public string? AccessTokenHash { get; set; }
    public DateTime? AccessTokenExpires { get; set; }

    public byte[]? RefreshTokenEnc { get; set; }
    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpires { get; set; }

    public byte[]? IdTokenEnc { get; set; }
    public string? IdTokenHash { get; set; }

    public string? Scope { get; set; }
    public string? TokenType { get; set; }
    public string? CorrelationId { get; set; }
    public string HashAlgoritmo { get; set; } = "SHA256";

    public int Version { get; set; } = 1;
    public bool Activo { get; set; } = true;
    public bool Revocado { get; set; }
    public DateTime? FechaRenovacion { get; set; }
    public DateTime? UltimoUso { get; set; }
    public DateTime? FechaRevocacion { get; set; }
    public string? MotivoRevocacion { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public IdenExt? IdenExt { get; set; }

    public static IdenExtTokens Crear(
        long idIdenExt,
        byte[]? accessTokenEnc = null, string? accessTokenHash = null, DateTime? accessTokenExpires = null,
        byte[]? refreshTokenEnc = null, string? refreshTokenHash = null, DateTime? refreshTokenExpires = null,
        byte[]? idTokenEnc = null, string? idTokenHash = null,
        string? scope = null, string? tokenType = null, string? correlationId = null,
        string hashAlgoritmo = "SHA256", int version = 1)
    {
        return new IdenExtTokens
        {
            IdIdenExt = idIdenExt,
            AccessTokenEnc = accessTokenEnc,
            AccessTokenHash = accessTokenHash,
            AccessTokenExpires = accessTokenExpires,
            RefreshTokenEnc = refreshTokenEnc,
            RefreshTokenHash = refreshTokenHash,
            RefreshTokenExpires = refreshTokenExpires,
            IdTokenEnc = idTokenEnc,
            IdTokenHash = idTokenHash,
            Scope = scope,
            TokenType = tokenType,
            CorrelationId = correlationId,
            HashAlgoritmo = hashAlgoritmo,
            Version = version,
            Activo = true,
            Revocado = false
        };
    }
}
