# AGENTS.md

Project-wide rules for AI agents and human contributors. If a nested `AGENTS.md` exists, it overrides these rules for its directory.

## Repository layout

- `RoomToRentRadar/` — .NET MAUI app (Android, iOS, Mac Catalyst, Windows)
- `RoomToRentRadar.Shared/` — wire DTOs shared by the API and the app
- `backend/` — hexagonal ASP.NET Core backend:
  - `RoomToRentRadar.Domain/` — entities and domain enums
  - `RoomToRentRadar.Ports/` — inbound/outbound ports and use-case models
  - `RoomToRentRadar.UseCases/` — application services orchestrating the ports
  - `RoomToRentRadar.SqliteDataAccess/` — EF Core + SQLite adapter
  - `RoomToRentRadar.Infrastructure/` — external adapters (photos, tokens, routing)
  - `RoomToRentRadar.Api/` — HTTP inbound adapter + composition root
  - `RoomToRentRadar.Api.Tests/` — API unit and integration tests
- `RoomToRentRadarUnitTests/` — mobile ViewModel unit tests
- `docs/ARCHITECTURE.md` — authoritative architecture and structure guide
- `scripts/` — local dev/QA helpers (not part of the build)
- `.agents/skills/` — canonical skills shared by all tools (also auto-discovered by Codex)
- `.claude/skills/`, `.claude/agents/` — Claude Code mirrors
- `.opencode/agent/` — opencode subagents
- `.codex/agents/` — Codex custom subagents
- `.qoder/skills/`, `.qoder/agents/` — Qoder IDE/CLI mirrors
- `opencode.json` — opencode configuration

## Architecture and structure (mandatory)

`docs/ARCHITECTURE.md` is the source of truth for structure and contracts. Read it before changing code; update it in the same change whenever layout, dependencies or contracts change.

### Mobile app — layer-first + MVVM

- Keep the existing folders: `Constants/`, `Controls/`, `Extensions/`, `Helpers/`, `Models/`, `Services/`, `Validations/`, `ViewModels/`, `Views/`. Do not reintroduce `Core/`, `Features/`, `Pages/` or `PageModels/`, and do not add new top-level layers without explicit approval.
- Every page is exactly one `.xaml` file plus one `.xaml.cs` code-behind. Never add extra partial `.cs` files for a page.
- `ViewModels/` stays flat; use feature prefixes in type names (`HomeViewModel`, `ListingEditorViewModel`).
- Pages and ViewModels are resolved through DI. Register composition only in `Extensions/ServiceCollectionExtensions.cs` (`AddMobileInfrastructure` / `AddMobileFeatures`).
- ViewModels must not call static MAUI APIs (`Shell.Current`, `Application.Current`, `SecureStorage.Default`, `Connectivity.Current`). Extend the injected seams in `Services/Navigation`, `Services/Interactions`, `Services/Storage` instead.
- Every page that uses bindings sets `x:DataType`; every `DataTemplate` sets its own. `XC0022` and `XC0025` are build errors. The only exception is Mapsui rendering/gesture code-behind (`MapSearchPage`, `ListingEditorPage`).
- Routes live in `Constants/AppRoutes.cs`; page parameters flow to the ViewModel through `IQueryAttributable`.

### Backend — hexagonal (Ports & Adapters)

- Dependency rule: `Api → UseCases → Ports ← SqliteDataAccess / Infrastructure`, and everything may reference `Domain` and `Shared`. Never reference `Api` from lower layers, and never reference an adapter from `Ports` or `UseCases`.
- `Ports` and `UseCases` must never reference ASP.NET Core, EF Core, SQLite or adapter projects. HTTP-only types (`ProblemDetails`, `IFormFile`, status codes) stay in `Api`.
- Place new code by layer: ports and use-case models in `Ports`, orchestration in `UseCases`, EF queries and migrations in `SqliteDataAccess`, external calls in `Infrastructure`, controllers/validators/problem factories under `Api/Features/<Feature>`.
- Validation limits shared by API validators and EF configurations live once in `Ports/Models/<Area>` (for example `ListingLimits`, `PhotoUploadLimits`).
- Endpoints return `Shared` DTOs, never EF entities. Controllers map stable use-case error codes to `ProblemDetails`; use cases stay HTTP-agnostic.
- `Program.cs` is only a composition root; feature registration stays in the `Add*` extension methods.
- Schema changes use EF Core migrations; never use `EnsureCreated`.

## Structure changes

- Do not move, rename, add or delete projects, folders, solution entries or CI jobs unless the task explicitly asks for it.
- A structure change must update `RoomToRentRadar.slnx`, `.github/workflows/ci.yml` and `docs/ARCHITECTURE.md` together.
- Do not introduce additional architectural patterns (Clean Architecture redesign, vertical slices, MediatR, AutoMapper, extra layers) without explicit approval.
- Follow the pattern of the nearest existing file instead of inventing a new one.

## Git rules

### Never commit and push

- `main` is protected. Never commit, merge, or push directly to `main`.
- Never force-push `main` or any shared branch.
- AI agents never commit, push, or open pull requests. Only the human user does.

## Code quality

- `dotnet build` must succeed with no new warnings.
- Run the relevant tests before handing work over; add tests for behavior changes:
  - `dotnet test backend/RoomToRentRadar.Api.Tests/RoomToRentRadar.Api.Tests.csproj`
  - `dotnet test RoomToRentRadarUnitTests/RoomToRentRadarUnitTests.csproj`
- Tests use NUnit; follow the existing conventions in the test projects.
- Do not add NuGet packages without justification.
- Keep XAML and ViewModels consistent with the installed MAUI skills.
- Only change code required by the current task. Never edit, refactor, reformat, or delete code outside the task scope; report unexpected state instead of cleaning it up silently.

## Security

- Never commit secrets, API keys, tokens, connection strings, or personal data.
- Use user-secrets or environment variables for local configuration.
- Do not log sensitive data.
- Report security issues privately, not in public issues or PRs.

## Agent rules

- Read `docs/ARCHITECTURE.md` and the relevant `SKILL.md` under `.agents/skills/` before working in an area it covers.
- Delegate to the owning subagent:
  - `maui-ui` — XAML pages, styles, ViewModels, navigation UX
  - `maui-data` — mobile data, storage and local persistence
  - `dotnet-api` — backend ports, use cases, adapters, controllers and API tests
  - `maui-tester` — unit/UI tests and test data
  - `maui-reviewer` — review before handoff
- Never commit, push, or open a pull request unless explicitly asked.
- Never rewrite history or discard uncommitted user changes.
- Ask before adding dependencies, changing public APIs or contracts, or modifying protected branches.
- When a task is complete, report changed files, verification performed, and any follow-up.
