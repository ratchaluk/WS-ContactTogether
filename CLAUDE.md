# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ASP.NET Core Web API (`net10.0`, controller-based) over an existing SQL Server database for a
contact-center / service-request ("Contact Together") system. The project is at an early stage: the
EF Core data layer has been scaffolded from the database, but the only controller so far is the
template `WeatherForecastController`. There is no solution file, no test project, and the directory
is not a git repository.

## Commands

```powershell
dotnet build
dotnet run                          # uses the "http" launch profile -> http://localhost:5018
dotnet run --launch-profile https   # https://localhost:7066
dotnet watch run                    # hot reload
```

Swagger UI (`/swagger`) is only mapped when `ASPNETCORE_ENVIRONMENT=Development`; both launch
profiles set it.

[ContactTogetherApi.http](ContactTogetherApi.http) holds request samples runnable from VS / VS Code
REST clients.

### Connection string

`ConnectionStrings:DefaultConnection` is intentionally empty in
[appsettings.json](appsettings.json). The real value lives in user secrets
(`UserSecretsId` `5bd7ea0f-c854-4557-bbae-52312de693be`):

```powershell
dotnet user-secrets list
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<value>"
```

Keep the committed `appsettings*.json` free of credentials.

### JWT signing key

`Jwt:Key` is likewise empty in [appsettings.json](appsettings.json) and must come from user secrets.
Startup throws if it is shorter than 32 characters:

```powershell
dotnet user-secrets set "Jwt:Key" "<random value, 32+ chars>"
```

The rest of the `Jwt` section (`Issuer`, `Audience`, `ExpiresMinutes`) is safe to keep in
`appsettings.json`.

### Regenerating the data layer

Models and `ApplicationDbContext` are **scaffolded output** — do not hand-edit them; changes are
lost on the next scaffold. Re-scaffold after a schema change:

```powershell
dotnet ef dbcontext scaffold "Name=ConnectionStrings:DefaultConnection" Microsoft.EntityFrameworkCore.SqlServer `
  --context ApplicationDbContext --context-dir Data --output-dir Models --no-onconfiguring --force
```

There are no EF migrations and none should be added — the database is the source of truth
(database-first). `dotnet ef` (10.0.11) is installed.

## Architecture

- [Program.cs](Program.cs) — minimal top-level setup: controllers, `ApplicationDbContext` on SQL
  Server, JWT bearer authentication, Swagger (with a bearer security scheme) in Development, HTTPS
  redirection, `UseAuthentication()` → `UseAuthorization()`.
- [Auth/](Auth/) — hand-written authentication services, all registered in `Program.cs`:
  `JwtTokenService` issues tokens, `PasswordVerifier` checks `TblEmployee.UserPassword`, and
  `MemoryTokenRevocationStore` holds logged-out `jti` values. See "Authentication" below.
- [Data/ApplicationDbContext.cs](Data/ApplicationDbContext.cs) — single `partial` DbContext, ~30
  `DbSet`s, all mapping configured in `OnModelCreating`. It calls `partial void
  OnModelCreatingPartial(ModelBuilder)`: **put hand-written model configuration in a separate
  partial class implementing that method**, so re-scaffolding does not clobber it.
- [Models/](Models/) — one `partial` POCO per table, no data annotations (all configuration is
  fluent, in the context).
- [Dtos/](Dtos/) — hand-written request/response shapes. Never put these in `Models/`, which is
  scaffolded output.
- [Controllers/](Controllers/) — `[ApiController]` + `[Route("[controller]")]` convention.

### Authentication

[Controllers/AuthController.cs](Controllers/AuthController.cs) exposes `POST /Auth/login` (user name
+ password against `TblEmployee`, returns a JWT) and `POST /Auth/logout` (`[Authorize]`, revokes the
presented token). Endpoints opt in to protection with `[Authorize]`; everything else stays anonymous.

- **Passwords** — `TblEmployee.UserPassword` is an unconstrained `nvarchar(255)` (widened from
  `varchar(50)` on 2026-10-07 — the 84-char PBKDF2 hash did not fit).
  [Auth/PasswordVerifier.cs](Auth/PasswordVerifier.cs) treats a value that decodes to the ASP.NET
  Core PBKDF2 layout as a hash and everything else as plain text. That fallback is on by default;
  set `Auth:AllowLegacyPlaintextPasswords` to `false` once all rows are hashed. New passwords should
  be stored via `IPasswordVerifier.Hash`.
- **Logout** — JWTs are stateless, so logout records the token's `jti` in
  [Auth/MemoryTokenRevocationStore.cs](Auth/MemoryTokenRevocationStore.cs) until its `exp`, and the
  `OnTokenValidated` event in `Program.cs` rejects it from then on. The store is per-process: scaling
  out to more than one instance requires a shared store (Redis or a table).
- **Claims** — `sub` = `TblEmployee.Id`, `name` = `UserName`, `role` = `RoleId`, `org` =
  `OrganizationId`, `lang` = `DefaultLanguage`. Inbound claim mapping is disabled, so these arrive
  under exactly those names; constants live in [Auth/AuthClaimTypes.cs](Auth/AuthClaimTypes.cs).
  `role` carries the raw `RoleId`, so `[Authorize(Roles = ...)]` matches on ids, not role names —
  per-page rights still have to be read from `TblRolePage`.

### Domain shape

`TblService` is the central service-request entity (opened/closed dates, channel in/out, owner,
category, status, severity/priority/secrecy levels, area, organization), with `TblActivity` as its
per-request work log (composite key `ServiceId` + `Line`) and `TblReopenedLog` tracking reopenings
(composite key `SrId` + `Line`). `TblAccount`/`TblAccountDetail` is the caller/contact side;
`TblEmployee` + `TblRole` + `TblRolePage` is the agent side and per-page permission matrix
(`IsInsert`/`IsUpdate`/`IsDelete`/`IsSearch`/`IsDownload`/`IsPrint`/`IsOpen`/`IsAdmin`).
`TblRunning` drives formatted running numbers (`RunningFormat`, `RunningNext`) for codes such as
service-request numbers.

### Conventions inherited from the database

These are quirks of the existing schema, not choices to "clean up" in the models:

- **Flags are `string`, not `bool`** — `IsEnable`, `IsDefault`, `IsAdmin`, `TblAccount.IsScret` (sic), etc. are
  `nvarchar(10)`. Compare/assign them as strings; do not change the property types.
- **Keys are `string`** (`nvarchar(50)`), not `int`/`Guid`, across nearly every table.
  New rows get a UUID key automatically (32 upper-case hex chars, no dashes): `ApplicationDbContext.Keys.cs`
  attaches `GuidStringValueGenerator` to every single-string primary key, so leave `Id` unset when
  inserting. Use `GuidStringValueGenerator.NewId()` if you need an id before `Add()`.
- **Lookup tables are self-referencing hierarchies** via `RefId` → `Ref` / `InverseRef`:
  `TblCategory`, `TblArea`, `TblStatus`, `TblOrganization`. The root/parent convention matters when
  querying these.
- **Bilingual columns** — most reference data carries `NameTh` / `NameEn` (and
  `SalutationTh`/`FirstnameTh`/… on people). The DB collation is `Thai_100_CS_AI` (case-sensitive,
  accent-insensitive), so string comparisons executed in SQL are case-sensitive.
- Most navigation properties were **not** scaffolded because the schema lacks the corresponding FK
  constraints — only a handful exist (`TblAccountDetail→TblAccount`, `TblActivity→TblCategory`,
  `TblService→TblStatus`, `TblRolePage→TblRole`, `TblReportParameter→TblReport`,
  `TblBroadcastGroup→TblBroadcast`, plus the self-references). Expect to join on ID columns
  manually rather than assuming a navigation exists.
- `TblAccount1` maps the keyless legacy import table `TblAccounts` (columns literally named
  `Column1 Address`, etc.) — an import staging artifact, distinct from `TblAccount`. Likewise
  `TblAccount.Created`/`Updated`/`Birthdate` are `string` there while other tables use `DateTime`.

## Cleanup left from the template

[WeatherForecast.cs](WeatherForecast.cs),
[Controllers/WeatherForecastController.cs](Controllers/WeatherForecastController.cs), and the
sample request in the `.http` file are scaffolding from `dotnet new webapi` and can be deleted once
real endpoints exist.
