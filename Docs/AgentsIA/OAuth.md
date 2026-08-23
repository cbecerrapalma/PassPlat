# OAuth

## Purpose
External identity using server-side Authorization Code + PKCE.

## Mandatory Rules
- HTTPS, state, nonce and authorization-code replay protection are mandatory.
- Callback URI comes from persisted provider configuration; secrets/tokens are encrypted.
- Providers resolve through DI, not switches; production stores must be scalable.

## Load Next
[Security](Security.md), [Authentication](Authentication.md), [WebAPI](WebAPI.md).
