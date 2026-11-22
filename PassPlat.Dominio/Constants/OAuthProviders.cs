namespace PassPlat.Dominio.Constants;

public static class OAuthProviders
{
    public const string Google    = "GOOGLE";
    public const string Microsoft = "MICROSOFT";
    public const string GitHub    = "GITHUB";
    public const string Facebook  = "FACEBOOK";
    public const string Instagram = "INSTAGRAM";

    public static readonly string[] Todos = [Google, Microsoft, GitHub, Facebook, Instagram];
}
