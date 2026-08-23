# PassPlat — Agent Instructions

## 1. Mission

PassPlat is a .NET 10 identity platform for authentication, authorization, multi-tenancy, MFA, OAuth federation, audit, email, Outbox and Blazor WASM. Preserve clean boundaries, security and framework compatibility. `D:\CODIGOS\CBP\` is a reusable framework with real external consumer `D:\CODIGOS\InventaNet\`; do not optimize it solely for PassPlat and do not touch InventaNet.

## 2. Mandatory Workflow

Use this order when the MCP capabilities are available:

1. `sequential-thinking`: decompose, form at least five hypotheses, root-cause and plan.
2. `supermemory`: recover previous decisions and sprint continuity.
3. `sharplens`: symbols, dependencies, duplicates and designer safety.
4. `context7`: external APIs only.

Read this file, then load only the domain documents selected by the router below. Inspect relevant source and contracts before modifying code. Do not infer undocumented architectural changes.

## 3. STOP Conditions

- Stop on a failed required gate; record evidence and do not advance automatically.
- Stop before changing an inbound-referenced document until its links are resolved.
- Stop before deleting/moving Evidence or Framework documents without explicit approved design.
- Stop before changing CBP compatibility or an external consumer boundary.
- Stop if a task requires secrets, production data, external coordination, or a materially new product decision.
- A historical pending state is not current debt without current evidence.

## 4. Build & Test

After every code-file change, immediately run:

```powershell
cd D:\CODIGOS\PassPlat
dotnet build PassPlat.slnx
```

The build must end with **0 warnings and 0 errors**. Use the smallest relevant test gate; do not present historical counts as a current run.

```powershell
dotnet test PassPlat.slnx
cd tests
npx playwright test faseA19-switch-to-platform.spec.ts --reporter=list
npx playwright test faseA18-multitenant-gate.spec.ts --reporter=list
```

Playwright API endpoint is defined in `tests/api-config.ts` and can be overridden through `API_BASE_URL`.

## 5. Repository / Solution Map

```text
PassPlat.Dominio          entities, factories, enums, pure domain rules
PassPlat.Datos            EF Core, DbContext, repositories, SP/RawQuery
PassPlat.Aplicacion       DTOs, validators, services, events, security, email
PassPlat.Aplicacion.Dtos  shared DTOs
PassPlat.WebAPI           HTTP, middleware, DI, JWT, controllers
PassPlat.Web              Blazor WASM, MudBlazor UI
CBP                        reusable framework outside this repository
```

Dependency direction is `Aplicacion → Datos → Dominio`; the domain never depends on WebAPI, UI, SQL-specific implementation, Serilog or HTTP.

## 6. Architecture Rules

- CBP framework components stay decoupled and compatible with consumers beyond PassPlat.
- `EventBase` and DI event dispatch are the event baseline.
- Each public async repository method returns `Task<Result<T>>` and catches DB failures as `DB_ERROR`.
- Read `Result` failures before consuming `Value`; propagate failures across repository, service, controller and UI.
- `RepositoryAsync<TEntity>` uses one generic argument. `GetByIdAsync` returns `Result<TEntity>`.
- Services access data through repositories, never `DbContext`/`DbSet` directly.
- A write flow has exactly one commit owner. Do not add a redundant `SaveChangesAsync`.
- Controllers inherit `BaseApiController` and use `FromResult`, `FromResultQuery` or `CreatedFromResult`.

## 7. Security Rules

- Password hashing uses Argon2id; peppers are external/versioned.
- Encrypt secrets and provider refresh tokens; never log/copy credentials, tokens, SQL parameters or protected PII.
- OAuth uses HTTPS, Authorization Code + PKCE S256, state and nonce, replay protection, server-side callback and DI provider factory.
- OAuth redirect URI comes only from persisted `ConfProvIden.Callback`; never construct it from request host/scheme.
- OIDC validation includes signature/JWKS, issuer, audience, lifetime and nonce; clock skew maximum is five minutes.
- OAuth production state/code stores must be persistent/scalable; no production `ConcurrentDictionary`.
- Local MFA remains applicable after external authentication.

## 8. Data / SQL Rules

- SQL sources of truth: `D:\CODIGOS\BBDD\PASSWORDS.sql` and `PASSWORDS SP.sql`.
- Entities use `Id` PKs, `Id{Table}` FK names, Spanish properties, `ICollection<T> = []`, and static factories.
- Enums begin with `E`. Tables match SQL exactly (`IPs`, `Disp`).
- EF configurations live one per entity under `Configurations`; use SQL datetime defaults, stored computed columns, filtered/descending indexes and `DeleteBehavior.Restrict`.
- `tinyint`, `smallint`, `bigint`, `uniqueidentifier` map to `byte`, `short`, `long`, `Guid`.
- Use `IRawQueryRepositoryAsync`; `QuerySPAsync<T>` returns a list—check failure and extract the expected row.
- Custom repositories extend `IRepositoryAsync<TEntity>` and are registered individually.

## 9. Testing Rules

- Verify a code change proportionately with build and relevant unit/architecture/E2E tests.
- Treat `Docs/Evidence/` and `Docs/audit/` as immutable evidence; it records results rather than defining rules.
- Do not change test baselines merely to make a gate pass.

## 10. Documentation Rules

- All reports and design documents live in `Docs/`.
- `Docs/Architecture/` is current enduring architecture; `Docs/Framework/` stable CBP contracts; `Docs/Sprints/SXX/` historical delivery records; `Docs/Evidence/` immutable evidence.
- `Docs/Sprints/ARCHIVE/` and `ANCHORED-SUMMARIES-ARCHIVE.md` are historical lookup only, never default task context.
- AgentsIA documents are concise operational routers. They link to canonical sources and never duplicate contracts or sprint narratives.
- Resolve inbound references before a file move/rename/delete. When Git is unavailable, use SHA-256 manifests and physical backups.

## 11. Agent Knowledge Router

| Task | Load |
|---|---|
| Architecture / dependency decision | `Docs/AgentsIA/Architecture.md`, `Contracts.md` |
| Domain | `Domain.md`, `Contracts.md` |
| EF, repository, RawQuery or SQL | `Development.md`, `Data.md`, `Database.md`, `Contracts.md` |
| Application service | `Development.md`, domain-specific guide, `Contracts.md` |
| Auth / MFA | `Authentication.md`, `Security.md`, `Email.md` |
| Authorization / tenant scope | `Authorization.md`, `MultiTenant.md`, `Security.md` |
| OAuth | `OAuth.md`, `Security.md`, `Authentication.md`, `WebAPI.md` |
| Email / background / Outbox | `Email.md`, `BackgroundJobs.md`, `Outbox.md`, `Observability.md` |
| Events / logging | `Events.md`, `Logging.md`, `Observability.md`, `Contracts.md` |
| Cache | `Caching.md`, `MultiTenant.md`, `Observability.md` |
| WebAPI | `WebAPI.md`, `Security.md`, `Testing.md` |
| UI | `UI.md`, `Development.md`, `Testing.md` |
| Tests | `Testing.md`, `Contracts.md` |
| CBP framework | `Framework/README.md` then only its component guide |

The routing root is [Docs/AgentsIA/README.md](Docs/AgentsIA/README.md). Load Sprint history only after a selected AgentsIA document identifies an actual historical dependency.

## 12. Current Baseline

- PassPlat build: 2026-08-19, 0 warnings / 0 errors after duplicate `Program.cs` imports were removed.
- PassPlat test discovery: 210 tests (56 architecture, 154 application); record a fresh completed run for any certification claim.
- CBP build: 2026-08-19, 0 warnings / 0 errors; password tests 56/56.
- S44: S44.0 discovery and S44.1 design complete; S44.2 implementation in progress; S44.3 not started.

For navigation, use [AgentsIA](Docs/AgentsIA/README.md), [permanent docs](Docs/AgentsIA/Navigation/PermanentDocs.md), [sprints](Docs/AgentsIA/Navigation/Sprints.md), [evidence](Docs/AgentsIA/Navigation/Evidence.md), and [troubleshooting](Docs/AgentsIA/Navigation/Troubleshooting.md).
