# Background Jobs

## Purpose
Hosted-service lifecycle, status and safe background execution.

## Mandatory Rules
Background work has no request context unless it is explicitly propagated; use a safe correlation fallback.

## Load Next
[Observability](Observability.md), [Email](Email.md), [Outbox](Outbox.md).
