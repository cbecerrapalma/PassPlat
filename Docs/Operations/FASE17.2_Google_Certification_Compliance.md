# FASE 17.2 — Google OAuth2 Compliance Matrix

## Overview

Google Identity Provider certification through 59 xUnit tests covering authorization, token validation, security, resilience, concurrency, and performance. All tests pass with 0 build errors and 0 warnings.

## Compliance Matrix

| # | Requirement | RFC | Test Coverage | Status | Evidence |
|---|-------------|-----|--------------|--------|----------|
| 1 | Authorization URL generation | RFC 6749 §4.1 | `AuthorizationUrlTests` (10 tests) | ✅ PASS | Validates client_id, redirect_uri, scope, state, nonce, PKCE, offline access |
| 2 | PKCE S256 support | RFC 7636 §4 | `ValidateToken_SendsCodeVerifierInTokenRequest` | ✅ PASS | `code_verifier` sent in token request body |
| 3 | State + Nonce validation | RFC 6749 §10.12, OIDC Core §3.1.2.6 | `ValidateToken_NonceMismatch`, `ValidateToken_NullNonce` | ✅ PASS | Nonce mismatch rejected, null nonce skips validation |
| 4 | IdToken signature validation | OIDC Core §3.1.3.7 | `ValidateToken_InvalidSignature`, `AlgNone_IsRejected`, `AlgHS256_IsRejected` | ✅ PASS | Invalid key → SIGNATURE_INVALID; alg=none → PROVIDER_ERROR; HS256 rejected |
| 5 | Issuer validation | OIDC Core §3.1.3.7 | `ValidateToken_InvalidIssuer` | ✅ PASS | Wrong issuer → ISSUER_MISMATCH |
| 6 | Audience validation | OIDC Core §3.1.3.7 | `ValidateToken_InvalidAudience`, `MultipleAudiences_ValidWhenClientIdPresent` | ✅ PASS | Wrong aud → AUD_MISMATCH; multi-aud accepted when clientId present |
| 7 | Token expiry validation | OIDC Core §3.1.3.7 | `ValidateToken_ExpiredToken`, `TokenWithoutExp` | ✅ PASS | Expired → TOKEN_EXPIRED; missing exp rejected |
| 8 | Required claims (sub) | OIDC Core §2 (obligatorio) | `TokenWithoutSub_IsRejected` | ✅ PASS | Missing sub → MISSING_SUB_CLAIM error |
| 9 | Clock skew tolerance | OIDC Core §3.1.3.7 | `ClockSkew_WithinLimit_Accepts`, `ClockSkew_BeyondLimit_Rejects` | ✅ PASS | <5 min skew accepted, >5 min rejected |
| 10 | JWKS key rotation | RFC 7517 | `KidNotFound_ReturnsSignatureInvalid`, `EmptyJwks_ReturnsSignatureInvalid` | ✅ PASS | Unknown kid → SIGNATURE_INVALID; empty JWKS → SIGNATURE_INVALID |
| 11 | HTTP error resilience | — | `HttpError_ReturnsFailure`, `Timeout_ReturnsFailure`, `MalformedJson_ReturnsFailure` | ✅ PASS | 500, timeout, bad JSON → PROVIDER_ERROR |
| 12 | Refresh token flow | RFC 6749 §6 | `RefreshTokenTests` (6 tests) | ✅ PASS | Valid refresh, missing token, HTTP error, timeout, bad JSON, named client |
| 13 | Concurrency safety | — | `ConcurrentTokenValidation_AllSucceed`, `ConcurrentCancellation_AllGraceful`, `ConcurrentMockStoreAccess_Safe`, `ConcurrentRefreshToken_Safe` | ✅ PASS | 20 concurrent validations, 10 cancellation, 50 mock store, 30 refresh |
| 14 | Thread-safe property access | — | `ConcurrentProviderPropertyAccess_Safe`, `ConcurrentAuthorizeUrl_Safe` | ✅ PASS | 100 concurrent property reads, 50 concurrent URL generations |
| 15 | Performance (URL generation) | — | `GenerateAuthorizationUrl_Performance` | ✅ PASS | 1000 iterations avg < 10ms (regression baseline, not SLA) |
| 16 | Performance (token validation) | — | `ValidateToken_Performance` | ✅ PASS | 100 iterations avg < 100ms (regression baseline, not SLA) |
| 17 | Performance (refresh token) | — | `RefreshToken_Performance` | ✅ PASS | 100 iterations avg < 50ms (regression baseline, not SLA) |
| 18 | Performance (descriptor access) | — | `DescriptorAccess_Performance` | ✅ PASS | 100k iterations avg < 10µs (regression baseline, not SLA) |
| 19 | Concurrent performance | — | `GoogleProvider_ConcurrentPerformance` | ✅ PASS | 50 concurrent validations < 10s |
| 20 | Offline access | RFC 6749 §4.1 | `AuthorizationUrl_ContainsOfflineAccess` | ✅ PASS | URL contains `access_type=offline&prompt=consent` |
| 21 | Provider descriptor | — | `AuthorizationUrl_UsesDescriptorCapabilities` | ✅ PASS | Descriptor provides static capabilities (PKCE, RefreshToken, Nonce, JWKS) |
| 22 | No hardcoded endpoints | — | `ValidateToken_ProviderCode_IsGoole` + code review | ✅ PASS | Endpoints from `ProvIden`/`ConfProvIden`; descriptor static only |

## Security Audit

| Check | Result | Notes |
|-------|--------|-------|
| `new HttpClient()` | ❌ NOT FOUND | All HTTP via Named Client `OAuth.Token` |
| `ConcurrentDictionary` | ❌ NOT FOUND | JWKS store uses `ICacheService` (CBP.Caching) |
| `IMemoryCache`/`IDistributedCache` | ❌ NOT FOUND | Cache only via `CBP.Caching.*` |
| Hardcoded secrets | ❌ NOT FOUND | ClientSecret from DB, RefreshToken encrypted AES-256-GCM |
| Switch/if-else provider selection | ❌ NOT FOUND | Provider Factory via DI + `IEnumerable<IExternalIdentityProvider>` |
| RedirectUri dynamic construction | ❌ NOT FOUND | RedirectUri from `ConfProvIden.Callback` |

## Test Results Summary

```
Total:  59
Passed: 59
Failed: 0
Skipped: 0
```

## Architecture Decisions Confirmed

| Decision | Evidence |
|----------|----------|
| `OAuthProviderDescriptor` for static capabilities only | All 7 providers use same pattern; test verifies descriptor |
| `IJwksStore` abstracted for testability | Tests use mock `IJwksStore` — no direct `JwksStore` instantiation |
| `TestHelpers.CreateMockHttpHandler` / `CreateMockHttpHandlerAsync` | Two overloads for sync/async response factories |
| `CreateProviderWithJwksAndToken` for end-to-end JWT validation tests | Used by 6 token validation tests |
| Error codes aligned with `GoogleIdentityProvider` catch blocks | All error code assertions match provider implementation |
