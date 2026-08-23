using Microsoft.Extensions.Logging;

namespace PassPlat.Aplicacion.Services.OAuth;

public static class OAuthEventIds
{
    // Callback flow (1-99)
    public static readonly EventId CallbackStarted      = new(101, "OAuth_Callback_Started");
    public static readonly EventId CallbackSessionFound  = new(102, "OAuth_Callback_SessionFound");
    public static readonly EventId CallbackSessionMiss   = new(103, "OAuth_Callback_SessionMiss");
    public static readonly EventId CallbackLoginResult   = new(104, "OAuth_Callback_LoginResult");
    public static readonly EventId CallbackRedirect      = new(105, "OAuth_Callback_Redirect");
    public static readonly EventId CallbackError         = new(106, "OAuth_Callback_Error");

    // LoginExterno flow (200-299)
    public static readonly EventId LoginStarted          = new(201, "OAuth_Login_Started");
    public static readonly EventId LoginConfigOk         = new(202, "OAuth_Login_ConfigOk");
    public static readonly EventId LoginSecretDecrypted  = new(203, "OAuth_Login_SecretDecrypted");
    public static readonly EventId LoginReplayCheck      = new(204, "OAuth_Login_ReplayCheck");
    public static readonly EventId LoginClaimsStarted    = new(205, "OAuth_Login_ClaimsStarted");
    public static readonly EventId LoginClaimsResult     = new(206, "OAuth_Login_ClaimsResult");
    public static readonly EventId LoginRepoResult       = new(207, "OAuth_Login_RepoResult");
    public static readonly EventId LoginTokenGenerated   = new(208, "OAuth_Login_TokenGenerated");
    public static readonly EventId LoginSessionCreated   = new(209, "OAuth_Login_SessionCreated");
    public static readonly EventId LoginCompleted        = new(210, "OAuth_Login_Completed");
    public static readonly EventId LoginError            = new(211, "OAuth_Login_Error");
    public static readonly EventId LoginAuditFailed      = new(212, "OAuth_Login_AuditFailed");

    // Token exchange (300-399)
    public static readonly EventId TokenRequest          = new(301, "OAuth_Token_Request");
    public static readonly EventId TokenResponse         = new(302, "OAuth_Token_Response");
    public static readonly EventId TokenResponseBody     = new(303, "OAuth_Token_ResponseBody");
    public static readonly EventId TokenJwksFetch        = new(304, "OAuth_Token_JwksFetch");
    public static readonly EventId TokenJwksResult       = new(305, "OAuth_Token_JwksResult");
    public static readonly EventId TokenValidation       = new(306, "OAuth_Token_Validation");
    public static readonly EventId TokenClaims           = new(307, "OAuth_Token_Claims");
    public static readonly EventId TokenNonceCheck       = new(308, "OAuth_Token_NonceCheck");

    // Timing (900-999)
    public static readonly EventId TimingStep            = new(901, "OAuth_Timing_Step");
}
