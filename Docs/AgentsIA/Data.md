# Data

## Purpose
EF repositories, configurations, stored procedures and UoW use.

## Mandatory Rules
- Repository public async methods return `Task<Result<T>>` and handle DB exceptions.
- Services do not access `DbContext`; repositories/services do not introduce an extra commit owner.

## Canonical References
[Repositories](../Datos/repositories.md), [UoW](../Datos/unit-of-work.md), [EF configuration](../Datos/ef-configuration.md).

## Load Next
[Database](Database.md), [Contracts](Contracts.md), [Framework data](Framework/CBPData.md).
