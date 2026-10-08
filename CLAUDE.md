# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ASP.NET Core Web API (`net10.0`, controller-based) over an existing SQL Server database for a
contact-center / service-request ("Contact Together") system. The EF Core data layer is scaffolded
from the database (database-first); endpoints are hand-written controllers on top of it. There is
no solution file and no test project (`FilesTest/` is a placeholder, not tests).

## Commands

```powershell
dotnet build
dotnet run                          # uses the "http" launch profile -> http://localhost:5018
dotnet run --launch-profile https   # https://localhost:7066
dotnet run watch                    # same as dotnet run (the app ignores "watch"); no hot reload
```

Swagger UI (`/swagger`) is only mapped when `ASPNETCORE_ENVIRONMENT=Development`; both launch
profiles set it. In Development, [Helper/SwaggerBrowserLauncher.cs](Helper/SwaggerBrowserLauncher.cs)
logs the Swagger URL and opens it in the browser on startup. Turn that off with
`Swagger:OpenBrowser=false` (e.g. `dotnet run -- --Swagger:OpenBrowser=false`).

[ContactTogetherApi.http](ContactTogetherApi.http) holds runnable request samples (VS / VS Code REST
clients). Its `login` request captures the token into `@token` for the requests after it; keep new
endpoint samples in that file.

### Secrets

`ConnectionStrings:DefaultConnection` and `Jwt:Key` are intentionally empty in
[appsettings.json](appsettings.json); the real values live in user secrets (`UserSecretsId`
`5bd7ea0f-c854-4557-bbae-52312de693be`). Startup throws if `Jwt:Key` is shorter than 32 characters.

```powershell
dotnet user-secrets list
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<value>"
dotnet user-secrets set "Jwt:Key" "<random value, 32+ chars>"
```

The rest of the `Jwt` section (`Issuer`, `Audience`, `ExpiresMinutes`) is safe to keep in
`appsettings.json`.

### Regenerating the data layer

Models and `ApplicationDbContext` are **scaffolded output**. Do not hand-edit them, because the
next scaffold overwrites the changes. Re-scaffold after a schema change:

```powershell
dotnet ef dbcontext scaffold "Name=ConnectionStrings:DefaultConnection" Microsoft.EntityFrameworkCore.SqlServer `
  --context ApplicationDbContext --context-dir Data --output-dir Models --no-onconfiguring --force
```

Keep both the `Name=` connection argument and `--no-onconfiguring`. Without them the scaffolder
writes an `OnConfiguring` override into `ApplicationDbContext` that hard-codes the connection
string, password included, and emits a `CS1030` `#warning`. That override runs after the options
from `AddDbContext` in `Program.cs`, so it silently replaces the user-secrets connection string. It
also puts the password into git. If it ever shows up, delete the whole `OnConfiguring` method
before committing. `Program.cs` already configures the context, and nothing calls the
parameterless constructor.

There are no EF migrations and none should be added; the database is the source of truth.

## Git workflow

From [README.md](README.md): work on a new branch per task, push it, open a pull request on GitHub,
and wait for approval. Delete the branch (remote and local `git branch -D`) after it is merged.
[.github/workflows/telegram-pr.yml](.github/workflows/telegram-pr.yml) posts PR and review events to
Telegram. It is the only CI and does not build or test anything.

The project skill `.claude/skills/git-commit-guide` (`<type>(<scope>): <subject>`, English subject,
72 chars or fewer, present tense) was copied from a Next.js project. Its type list applies here, but
its scopes, `.env`/Prisma gotchas and `npm run lint` checklist do not. Use scopes that match this
repo (e.g. `auth`, `account`, `report`, `user-management`, `lov`, `data`). Existing history mixes
these with free-form Thai messages.

## Architecture

- [Program.cs](Program.cs): controllers, `ApplicationDbContext` on SQL Server, JWT bearer auth,
  Swagger (with a bearer scheme) in Development, `UseAuthentication()` → `UseAuthorization()`.
- [Auth/](Auth/): hand-written auth services registered as singletons. `JwtTokenService`
  issues tokens, `PasswordVerifier` checks `TblEmployee.UserPassword`, and
  `MemoryTokenRevocationStore` holds logged-out `jti` values.
- [Data/ApplicationDbContext.cs](Data/ApplicationDbContext.cs): a single `partial` DbContext with
  all mapping in `OnModelCreating`. **Put hand-written model configuration in a separate partial
  class implementing `OnModelCreatingPartial`** so re-scaffolding does not clobber it.
- [Models/](Models/): one scaffolded `partial` POCO per table, no data annotations.
- [Dtos/](Dtos/): hand-written request/response shapes. Never put these in `Models/`.
- [Controllers/](Controllers/): `[ApiController]` + `[Route("[controller]")]`. Controllers
  inject `ApplicationDbContext` directly and query with LINQ. There is no service or repository
  layer.

### Controllers

- `AuthController`: `POST /Auth/login`, `POST /Auth/logout`.
- `UserManagementController` (`POST employees`), `AccountController` (`POST accounts`,
  `GET by-phone`), and `ListOfValuesController` (`GET channels`, lookup lists for UI drop-downs).
  These set the house style: namespaced DTOs in `Dtos/`, `[ProducesResponseType]`, a
  `CancellationToken`, `AsNoTracking()` for reads, `ValidationProblem(ModelState)` for bad input,
  and `MessageResponse` for other errors. Because the schema has no FKs, these controllers check
  lookup ids (`GenderId`, `RoleId`, `AreaId`, …) against their tables in a `ValidateLookupsAsync`
  helper before inserting.
- `ReportController`: `POST /Report/GetReport01`…`GetReport07`, each a large multi-join LINQ query
  over `TblService`. It is **anonymous at the moment** (`[Authorize]` is commented out). Its DTOs
  (`ServiceRequestReportRequest*`, `ServiceRequestReportDto`, …) live in
  [Dtos/RequestModel.cs](Dtos/RequestModel.cs) and [Dtos/ResponseModel.cs](Dtos/ResponseModel.cs)
  in the **global namespace**. Every report takes `ServiceRequestReportDateTimeRequest` (04 and 07
  take its subclasses `ServiceRequestReportRequestMainOrganization` and
  `ServiceRequestReportRequestService`): `P_Start`/`P_Finish` as ISO 8601 Thai
  local date-times, `yyyy-MM-ddTHH:mm:ss` (seconds optional, no offset or `Z`), parsed by
  `TryBuildDateTimeRange`. It uses `InvariantCulture` and does no time-zone conversion, so the
  server's locale and time zone do not affect it. The range includes the end value.
  - 01–04 and 07 filter `TblService.Created` by the full date-time.
  - 07 also requires `P_Service`, a top-level `TblCategory.Id` (Q&A, Claim, …), compared with
    `TblService.CategoryId`. Those top-level categories point `RefId` at themselves.
  - 04 also requires `P_MainOrg` and returns SRs whose organization is that main organization or
    one of its direct sub-organizations (`org.Id` or `org2.Id`).
  - 05/06 count contacts per day and use only the date part, because `TblContact.ContactStart`
    holds dates only. 05 counts contacts whose creator is in a claim team (`RefGroupId` 53/77). 06
    counts contacts whose creator is in none of 53/77/79/68. Both left-join a `DISTINCT` list of
    employee ids rather than joining the keyless `TblEmployeeGroup`/`TblOrganizationGroup` rows
    directly. An employee can sit in several groups, so the direct join counts a contact more than
    once and can put it in both reports.

#### Response envelope and status codes

`ReportController` wraps every response in `_0BaseReturn` ([Helper/0BaseReturn.cs](Helper/0BaseReturn.cs)):
`{ callAPIStatus, callAPIStatusMessage, result }`. The HTTP status carries the outcome, and the body
has no status-code field. `callAPIStatus` is `true` only for 2xx. `callAPIStatusMessage` is Thai
text that can be shown to users. Never put `ex.Message` in it. `[BaseReturnModelStateFilter]`
([Helper/BaseReturnModelStateFilter.cs](Helper/BaseReturnModelStateFilter.cs)) turns the automatic
`[ApiController]` 400 (malformed JSON, missing body) into a `_0BaseReturn` too. Apply it per
controller rather than changing `InvalidModelStateResponseFactory`, because that would also change
`ValidationProblem()` in the ProblemDetails-style controllers.

| HTTP | When | `callAPIStatus` | `result` |
|---|---|---|---|
| 200 | Success, **including a search or report with no rows** | `true` | data, or `[]` |
| 201 | A POST created a row | `true` | the created item |
| 400 | Bad input: missing field, wrong format, start after finish, malformed JSON | `false` | `null` |
| 401 | No token, an expired token, or a logged-out token | `false` | `null` |
| 403 | Signed in but lacks the page right (`TblRolePage`) | `false` | `null` |
| 404 | A specific resource by id does not exist (never for an empty report) | `false` | `null` |
| 409 | Duplicate or conflicting data, e.g. a username that already exists | `false` | `null` |
| 500 | Unexpected failure. Log it and answer `เกิดข้อผิดพลาดภายในระบบ` | `false` | `null` |

401/403 currently come from JwtBearer with an empty body. To return `_0BaseReturn` for them, handle
`OnChallenge`/`OnForbidden` in `JwtBearerEvents` when `[Authorize]` is turned on. A request the
client cancels (`OperationCanceledException`) is not caught and gets no envelope.
- `AdminController` is an empty shell. `WeatherForecastController` + [WeatherForecast.cs](WeatherForecast.cs)
  are leftover template code and can be deleted.

When inserting rows, follow the existing conventions. Generate ids as
`Guid.NewGuid().ToString("N").ToUpperInvariant()`, and take `CreatedBy`/`UpdatedBy` from the
token's `sub` claim, never from the request body.

### Authentication

Endpoints opt in with `[Authorize]`; anything without it is anonymous.

- **Passwords**: `TblEmployee.UserPassword` is an unconstrained `nvarchar(255)`.
  `PasswordVerifier` treats a value that decodes to the ASP.NET Core PBKDF2 layout as a hash and
  anything else as plain text. That plain-text fallback is on by default; set
  `Auth:AllowLegacyPlaintextPasswords` to `false` once every row is hashed. Store new passwords via
  `IPasswordVerifier.Hash`.
- **Logout**: JWTs are stateless, so logout records the token's `jti` until its `exp`, and
  `OnTokenValidated` in `Program.cs` rejects it from then on. The store is per-process, so running
  more than one instance requires a shared store (Redis or a table).
- **Claims**: `sub` = `TblEmployee.Id`, `name` = `UserName`, `role` = `RoleId`, `org` =
  `OrganizationId`, `lang` = `DefaultLanguage`. Inbound claim mapping is disabled, so these arrive
  under exactly those names (constants are in [Auth/AuthClaimTypes.cs](Auth/AuthClaimTypes.cs)).
  `role` is the raw `RoleId`, so `[Authorize(Roles = ...)]` matches ids, not names. Per-page rights
  must be read from `TblRolePage`.

### Domain shape

`TblService` is the central service-request entity (opened/closed dates, incoming/outgoing
channel, owner, category, status, severity/priority/secrecy levels, area, organization).
`TblActivity` is its per-request work log (composite key `ServiceId` + `Line`; `ContactId` →
`TblContact` holds the caller's number). `TblReopenedLog` tracks reopenings (composite key
`SrId` + `Line`). `TblAccount`/`TblAccountDetail` is the caller/contact side. In `TblAccountDetail`,
`DetailType` is `MOBILE`/`HOME`/`OFFICE`/`FAX`/`MAIL`. `TblEmployee` + `TblRole` + `TblRolePage` is
the agent side and per-page permission matrix. `TblRunning` drives formatted running numbers for
codes such as service-request numbers. Reports resolve the "main organization" by joining
`TblOrganization` twice (`org.RefId` → parent).

### Conventions inherited from the database

These are quirks of the existing schema. Do not "clean them up" in the models:

- **Flags are `string`, not `bool`.** `IsEnable`, `IsDefault`, `IsAdmin`, `TblAccount.IsScret`
  (sic), etc. are `nvarchar(10)` holding `"T"`/`"F"`.
- **Keys are `string`** (`nvarchar(50)`), in practice 32-char upper-case dashless GUIDs.
- **Dates are mixed**: some tables have `DateTime` columns, and many have `string` columns with
  **different formats per table**. Match the format already in the table, because the legacy
  application parses these:
  - `TblAccount.Created`/`Updated`: `M/d/yyyy H:mm`
  - `TblEmployee.Created`/`Updated`: `yyyy-MM-dd HH:mm:ss`; `Birthdate`/`DateHire`: `yyyy-MM-dd`
  - `TblService.DateOpened`: `MM/dd/yyyy HH:mm`

  `TblService.Created`/`Updated` and `TblContact.ContactStart`/`ContactEnd` are `datetime2`, so the
  reports filter them with plain `DateTime` comparisons. They used to be `MM/dd/yyyy` text, and
  older code that compares them with `string.Compare` was wrong across years.
- **Lookup tables are self-referencing hierarchies** via `RefId` → `Ref` / `InverseRef`
  (`TblCategory`, `TblArea`, `TblStatus`, `TblOrganization`). Reports exclude service requests whose
  status's `RefId` is a specific parent id.
- **Bilingual columns**: `NameTh`/`NameEn`, `SalutationTh`/`FirstnameTh`/…. The DB collation
  is `Thai_100_CS_AI`, so string comparisons run in SQL are **case-sensitive**.
- **Few navigation properties exist** because the schema lacks most FK constraints. Join on id
  columns manually; left joins use `join … into g from x in g.DefaultIfEmpty()`.
- `TblAccount1` maps the keyless legacy import table `TblAccounts` (columns literally named
  `Column1 Address`, etc.). It is a staging artifact, distinct from `TblAccount`.
