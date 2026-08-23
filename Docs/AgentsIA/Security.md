# Security

## Purpose
Cross-cutting password, encryption, authorization and safe logging rules.

## Mandatory Rules
- Password hashing is Argon2id; secrets stay external or encrypted.
- Do not log credentials, token values, protected PII or SQL parameters.
- Preserve Result failures; never weaken validation to hide them.

## Load Next
[Authentication](Authentication.md), [Authorization](Authorization.md), [OAuth](OAuth.md), or [Email](Email.md).
