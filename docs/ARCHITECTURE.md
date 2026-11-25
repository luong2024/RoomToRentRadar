# RoomToRentRadar — Architecture Guide (Group 1)

Practical architecture and structure rules for the group-1 codebase. This document describes what the
repository actually uses today. Update it when an architecture decision changes.

## 1. Architecture decisions

| Area | Decision |
| --- | --- |
| Mobile app | Layer-first folders + MVVM (CommunityToolkit.Mvvm) |
| Backend | Hexagonal architecture (Ports & Adapters); ASP.NET Core is the inbound HTTP adapter |
| Database | EF Core 10 + SQLite, hidden behind repository ports |
| Contracts | `RoomToRentRadar.Shared` — DTOs shared by API and mobile app |
| Communication | REST/JSON, cursor pagination |
| DI | Microsoft.Extensions.DependencyInjection everywhere |
| Tests | NUnit 4 for unit, integration and UI tests |

## 2. Repository layout

```text
RoomToRentRadar.slnx
RoomToRentRadar/                  .NET MAUI app (Android, iOS, Mac Catalyst, Windows)
RoomToRentRadar.Shared/           DTOs shared by API and app
backend/
├── RoomToRentRadar.Domain/            EF Core entities and domain enums
├── RoomToRentRadar.Ports/             Inbound/outbound ports and use-case models
├── RoomToRentRadar.UseCases/          Application services orchestrating the ports
├── RoomToRentRadar.SqliteDataAccess/  EF Core DbContext, migrations, repositories, seeding
├── RoomToRentRadar.Infrastructure/    External adapters (photos, tokens, routing, background jobs)
├── RoomToRentRadar.Api/               ASP.NET Core API — inbound adapter + composition root
└── RoomToRentRadar.Api.Tests/         API unit + integration tests
RoomToRentRadarUnitTests/         Mobile ViewModel unit tests
docs/                             Architecture and process documentation
```

Do not move projects into `src/` / `tests/` without updating the solution and CI.

## 3. Mobile application (`RoomToRentRadar/`)

### 3.1 Folder structure

```text
RoomToRentRadar/
├── Constants/            API endpoints and Shell routes
├── Controls/             Reusable XAML controls
├── Extensions/           IServiceCollection composition (AddMobileInfrastructure/AddMobileFeatures)
├── Helpers/              Stateless helpers (media URLs, directions URLs)
├── Models/               App-only models (not API DTOs)
├── Services/
│   ├── Api/              HTTP clients and API wrappers
│   ├── Auth/             Authentication, token store, HTTP handler
│   ├── ErrorHandling/    IErrorHandler implementations
│   ├── Interactions/     Alerts, snackbars, undo
│   ├── Navigation/       Shell navigation abstraction
│   ├── Storage/          Local preference stores
│   └── ...               App services (favourites, comparison, security score)
├── Validations/          Form and photo validation
├── ViewModels/           All page view models
├── Views/                All pages
├── Platforms/            Platform-specific code
├── Resources/            Styles, images, fonts
├── App.xaml, AppShell.xaml, MauiProgram.cs
```

Create a folder only when it contains code. Do not add empty layers.

- A page is exactly one `.xaml` file plus one `.xaml.cs` code-behind. Do not add extra partial
  `.cs` files for a page; move logic to the ViewModel or a helper instead.
- The ViewModels folder is flat; keep feature prefixes in type names (`HomeViewModel`,
  `ListingEditorViewModel`) instead of nested feature folders.

### 3.2 MVVM rules

- ViewModels derive from `ObservableObject` and use `[ObservableProperty]` / `[RelayCommand]`.
- ViewModels must not call static MAUI APIs (`Shell.Current`, `Application.Current`,
  `SecureStorage.Default`, `Connectivity.Current`). Inject an interface instead.
- Pages receive their ViewModel through constructor injection and assign `BindingContext`.
- Every page that uses bindings sets `x:DataType`; every `DataTemplate` sets its own. `XC0022` and
  `XC0025` are build errors. `DirectionsPage` has no bindings, so it sets neither.
- Binding failures and UI state belong to the ViewModel; code-behind only wires lifecycle events.
  Exception: native map rendering and gestures (`MapSearchPage`, `ListingEditorPage` Mapsui) stay
  in code-behind because they drive the map control directly; data still comes from the ViewModel.

### 3.3 Dependency injection

- Pages and ViewModels: `AddTransient`.
- Stateless/shared services (HTTP client, API client, error handler): `AddSingleton`.
- Do not use `AddScoped` in the mobile app.
- Register features through `AddMobileFeatures()` and infrastructure through
  `AddMobileInfrastructure()`; `MauiProgram` only composes them.
- `AppShell` resolves pages through `IServiceProvider` so constructor injection keeps working.

### 3.4 Navigation

- Shell navigation only; no `NavigationPage`.
- Every `ShellContent` has an explicit `Route`.
- Routes are declared in `AppRoutes` (`Constants/`) and registered in the `AppShell` constructor
  when they are not root shell content.
- Pass parameters to the ViewModel with `IQueryAttributable`, not to the page.

### 3.5 API communication

- All HTTP calls go through `Services/Api` — features never create `HttpClient` themselves.
- Base address: `http://localhost:5080` on Windows/iOS/Mac Catalyst, `http://10.0.2.2:5080` on the
  Android emulator. Android debug builds allow cleartext traffic for local development.
- Request/response types come from `RoomToRentRadar.Shared`.
- Failures are reported through `IErrorHandler`; do not show alerts from ViewModels.

### 3.6 Theming and layout

- Colors, styles and theme resources live in `Resources/Styles` and are consumed with
  `DynamicResource` where theme switching must apply.
- Never call `MergedDictionaries.Clear()`.
- Use .NET 10 `SafeAreaEdges`; do not use legacy `ios:Page.UseSafeArea`.
- Keep the light/dark toggle in the Shell flyout footer working.

## 4. Backend (`backend/`)

### 4.1 Structure

```text
backend/
├── RoomToRentRadar.Domain/            Entities and domain enums; references Shared only
├── RoomToRentRadar.Ports/             Ports and use-case models; references Domain + Shared
│   ├── Inbound/                       Use-case interfaces (IListingsUseCase, IModerationActionsUseCase, ...)
│   ├── Outbound/Repositories/         Repository and IUnitOfWork ports
│   ├── Outbound/Services/             External service ports (IPhotoStorage, IPasswordHasher, ...)
│   └── Models/<Area>/                 Commands, queries, result records, error codes and validation limits
├── RoomToRentRadar.UseCases/          Application services; references Ports only
├── RoomToRentRadar.SqliteDataAccess/  EF Core adapter: AppDbContext, configurations, migrations,
│                                      repositories, seeding; references Domain + Ports + Shared
├── RoomToRentRadar.Infrastructure/    External adapters: photo storage/processing, tokens, routing,
│                                      background worker; references Ports
└── RoomToRentRadar.Api/               Inbound HTTP adapter + composition root
    ├── Features/<Feature>/            Controllers, request validators, ProblemDetails factories
    ├── Configuration/                 MVC/JSON, OpenAPI, CORS and reverse-proxy extensions
    └── Program.cs
```

Dependency rule: `Api → UseCases → Ports ← SqliteDataAccess / Infrastructure`, and everything may
reference `Domain` and `Shared`. `Ports` and `UseCases` must never reference ASP.NET Core, EF Core,
SQLite or the adapter projects. The API references all projects because it hosts the composition
root (`AddDataAccess`, `AddUseCases`, `AddInfrastructure`).

- `Program.cs` stays a composition root; keep feature registration in the extension methods above.
- Never return EF entities from endpoints. Use DTOs from `RoomToRentRadar.Shared`.
- HTTP-only types (`ProblemDetails`, `IFormFile`, status codes) stay in the API. Use cases report
  failure with `UseCaseResult<T>`/`ApplicationError` or a feature result record carrying a stable
  error code; the controller maps that code to the same `ProblemDetails` contract as before.
- Business rules (ownership checks, status transitions, paging decisions) live in `UseCases`.
  Repositories own EF queries, `ExecuteUpdateAsync` conditional updates and include graphs.
- Validation limits (max lengths, upload caps) live once in `Ports/Models/<Area>` (for example
  `ListingLimits`, `PhotoUploadLimits`) and are shared by API validators and EF configurations.
- Multi-write operations use `IUnitOfWork.BeginTransactionAsync`; the port exposes
  `CommitAsync` and async disposal so use cases stay storage-agnostic.

### 4.2 Adding a backend feature

1. `Ports`: add the inbound interface, the outbound repository/service interfaces and the request
   and result models for the feature.
2. `UseCases`: implement the inbound interface. Depend only on outbound ports; keep EF and HTTP out.
3. Adapters: implement repositories in `SqliteDataAccess` and external calls in `Infrastructure`.
4. `Api`: add the controller, request validator and problem factory under `Features/<Feature>`;
   register repositories in `AddDataAccess`, use cases in `AddUseCases` and adapters in
   `AddInfrastructure` (or a feature extension).
5. Cover the HTTP contract (status codes, problem `code` values, payload shape) with integration tests.

### 4.3 API rules

- MVC controllers with `[ApiController]` and attribute routing. Do not mix with Minimal APIs per feature.
- Errors use RFC 9457 `ProblemDetails` with a stable `code` extension.
- Validation happens at the boundary; invalid input produces 400 with `ProblemDetails`.
- OpenAPI is generated by the built-in .NET 10 support; Swagger UI is only a viewer.
- Schema changes use EF Core migrations. Never use `EnsureCreated`.
- Development startup applies migrations and seeds sample data; this is a documented exception.

### 4.4 Authentication and roles

- `POST /api/v1/auth/register` is public and accepts `student` or `landlord` only; admin accounts are provisioned by the system (demo seed / configuration), never self-registered.
- `POST /api/v1/auth/login` issues an opaque server-side session token (32 random bytes, stored as a SHA-256 hash, 7-day expiry). The bearer handler resolves it to claims on every request. Session tokens are chosen over JWT so logout revokes access immediately.
- Role wire values are `student`, `landlord`, `moderator` and `admin`; the token's role claim drives `ModeratorOrAdmin` RBAC.
- Passwords are stored as PBKDF2-SHA256 (100k iterations, 16-byte salt, fixed-time comparison).

## 5. Testing

- NUnit 4 with `NUnit3TestAdapter` in all test projects. Do not add xUnit.
- Test method names: `Method_Scenario_ExpectedBehavior`.
- Arrange/Act/Assert in every test.
- Mobile ViewModel tests use fakes for `IApiClient` / `IErrorHandler`; no Moq unless approved.
- API tests use `WebApplicationFactory<Program>` with SQLite in-memory; never mock `DbContext`.
- Every behavior change needs a test. Run the relevant suite before handing work over.

## 6. Conventions

- File-scoped namespaces, nullable enabled, async methods end with `Async`.
- Never use `.Result` / `.Wait()`; pass `CancellationToken` through I/O calls.
- Never commit secrets, connection strings, tokens or personal data.
- Local configuration uses user-secrets or environment variables.
- Do not add NuGet packages without justification.

## 7. Git and review

- `main` is protected. Work happens on feature branches and merges through a pull request.
- Commits follow Conventional Commits and contain one logical change.
- AI agents never commit, push, or open pull requests. Only the human user does.
- Out-of-scope edits are not allowed: change only what the current task requires.

## 8. Definition of done

- `dotnet build` succeeds with no new warnings.
- Relevant unit/integration tests pass.
- The feature is manually verified (app runs, API responds) or the limitation is reported.
- Documentation is updated when structure or contracts change.
