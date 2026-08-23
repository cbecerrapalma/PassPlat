# Domain

## Purpose
Entities, enums, factories and domain invariants.

## Mandatory Rules
- Keep the domain independent of WebAPI, EF/SQL, Serilog and HTTP.
- Use Spanish names, `E`-prefixed enums and entity factory methods.

## Canonical References
[Domain conventions](../Domain/conventions.md), [Architecture](Architecture.md).

## Load Next
[Contracts](Contracts.md). Do not load Data or UI unless the boundary changes.
