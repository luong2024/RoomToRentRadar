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
│   ├── Media/            Device media picking (profile avatar)
│   ├── Navigation/       Shell navigation abstraction
│   ├── Profile/          Session-scoped profile details store
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
- Registration is two-step on the client too: `RegisterPage` starts the challenge and navigates to
  `OtpVerificationPage` with the challenge id and email. After sign-in or a verified registration the
  app calls `INavigationService.GoToRoleRootAsync(role)`:
  Student → `//home`, Landlord → `//my-listings`, Moderator/Admin → `//moderation`.
- `AppShell` shows the `MyListings` tab only for Landlord, the `Moderation` tab only for
  Moderator/Admin, and the `Profile` (`Tài khoản`) tab for every role. It observes
  `AuthService.SessionChanged` and only touches tab visibility on the main thread.
- The Home account button opens the Profile tab when signed in and the login page when signed out.
  `ProfilePage` loads through `IProfileClient` (`GET /api/v1/auth/me`) and shows signed-out and error
  states of its own.
- The Profile tab is role-adaptive: Students see personal and rental-need information, Landlords see
  business details, verification, room stats and reputation, and staff see account identity plus the
  settings entry point. Sections the API does not expose yet read from `IProfileDetailsStore`
  (in-memory, per session, seeded per user) and are captioned as illustrative data.
- `EditProfilePage` (`AppRoutes.EditProfile`) is opened from the Profile tab for Students and
  Landlords. It saves the display name through `PUT /api/v1/auth/me` and the remaining form fields
  through `IProfileDetailsStore`; the avatar is chosen from the device gallery via `IAvatarPicker`
  and copied under `FileSystem.AppDataDirectory`.
- `AccountSettingsPage` (`AppRoutes.AccountSettings`) hosts notification/privacy toggles persisted in
  `IProfileDetailsStore`, placeholder rows for features not shipped yet, and the real sign-out.
- "Phòng yêu thích" opens the Favourites tab and "Tin đăng của tôi" opens My Listings; the remaining
  profile menu entries are coming-soon alerts.

### 3.5 API communication

- All HTTP calls go through `Services/Api` — features never create `HttpClient` themselves.
- Base address: `http://localhost:5080` on Windows/iOS/Mac Catalyst, `http://10.0.2.2:5080` on the
  Android emulator. Android debug builds allow cleartext traffic for local development.
- Request/response types come from `RoomToRentRadar.Shared`.
- Failures are reported through `IErrorHandler`; do not show alerts from ViewModels.
- `AuthService` owns the session: it stores the token and role through `ITokenStore`
  (`AppUserRole`), exposes `CurrentRole` and `CurrentDisplayName`, and raises `SessionChanged`. ViewModels
  read the role from the service, never from secure storage directly. Sessions stored before roles
  existed are treated as Student. After a profile rename `EditProfileViewModel` calls
  `UpdateCurrentDisplayName` so the cached name stays in sync; the Profile tab also refreshes it on load.

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
│                                      background worker; references Ports + Shared
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
  `Shared/Auth/AccountRules` is the deliberate exception: the mobile client cannot reference
  `Ports`, so every registration limit (email/display name/password lengths, OTP length, lifetime,
  attempts and the hash/salt column lengths) lives in `Shared` and is shared by the API validator,
  EF configurations and the mobile form.
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

### 4.4 Authentication, roles and authorization

- Roles are `student`, `landlord`, `moderator` and `admin`. The `UserRole` enum and its
  `UserRoleWire` values live once in `Shared/Auth` (like `ListingStatus`); the `users` CHECK
  constraint, EF converters, the token role claim, `AccountRules` and the mobile `AppUserRoleWire`
  all derive from that single source.
- Registration is a two-step, email-verified flow for `student` and `landlord` only; moderator and
  admin accounts are provisioned out of band. `POST /api/v1/auth/register` validates email
  format/length, display name length and a password of 8-128 characters containing letters and
  digits (`RegistrationValidator`/`AccountRules`), hashes the password, generates a 6-digit OTP,
  stores both hashes in a `registration_challenges` row (10-minute lifetime, 5 attempts) and emails
  the code. A new registration for the same email first deletes that email's previous challenge, and
  expired challenge rows are pruned on the same path. It returns 202 with `RegisterChallengeResponse`;
  duplicate emails return 409 `email_already_registered`, including a concurrent insert caught by the
  unique-index handling, and a failed email hand-off returns 502 `email_delivery_failed` after the
  challenge is removed.
- `POST /api/v1/auth/register/verify` checks the challenge and OTP, creates the user, starts a
  session and returns `LoginResponse` so the client can redirect by role. Wrong codes consume an
  attempt atomically in the repository (`UPDATE ... WHERE attempt_count < 5`), so parallel
  submissions cannot exceed the cap. Unknown/expired challenges return 400 `invalid_otp_challenge`,
  a wrong code returns 400 `invalid_otp` with an `attempts_remaining` extension, exhaustion returns
  429 `otp_attempts_exceeded`, and an email that was registered in the meantime returns 409 after
  the challenge is deleted. The user insert, session insert and challenge delete run in one
  transaction. The challenge row stores only PBKDF2 hashes of the password and OTP.
- Email delivery goes through the `IEmailSender` port. `Auth:Email:Smtp` configures host, port,
  STARTTLS, timeout (default 8s, kept below the mobile client's 15s), credentials and from-address
  (`System.Net.Mail`, no extra package); outside Development a missing host fails startup, and so
  does STARTTLS disabled together with credentials. Development without SMTP falls back to
  `FileEmailSender`, writing `.eml` files under `<ContentRoot>/outbox` (gitignored) and logging only
  the path — never the OTP.
- `POST /api/v1/auth/login` issues an HS256-signed JWT access token (7-day expiry) carrying the user
  id, email, display name and role claims. The JwtBearer handler validates signature, issuer,
  audience and lifetime on every request. Each token is bound to a server-side `auth_sessions` row
  (stored as a SHA-256 hash) that is checked after signature validation, so logout still revokes
  access immediately.
- The signing key comes from `Auth:Token:SigningKey` (user-secrets or the
  `Auth__Token__SigningKey` environment variable). Missing keys fail startup outside Development;
  Development falls back to a per-process key.
- Passwords are stored as PBKDF2-SHA256 (100k iterations, 16-byte salt, fixed-time comparison)
  through `IPasswordHasher`.
- `POST /api/v1/auth/register` and `/login` share a fixed-window per-IP rate limit
  (`Auth:RateLimitPerMinute`, default 10); `/register/verify` has its own bucket
  (`Auth:VerifyRateLimitPerMinute`, default 20) so code retries do not consume the login/register
  budget. Exceeded requests answer 429 `auth_rate_limited` with a `Retry-After` header.
- Migration `AddStudentAndLandlordRoles` replaces the legacy `user` role: accounts that own at least
  one listing become `landlord` (they keep access to listing management and photo upload), all other
  legacy accounts become `student`. The `Down` migration merges both roles back to `user`.
- Authorization policies live in `Features/Auth/AuthPolicies.cs` and rely on the role claim issued by
  the JWT handler. `Landlord` guards listing management (`api/v1/listings` create/update/unpublish)
  and photo upload; `ModeratorOrAdmin` guards `api/v1/moderation/*`. Rejected calls return 401/403
  with a `ProblemDetails` `code`.
- The mobile moderation queue uses `GET /api/v1/moderation/listings?status=pending_review`
  (cursor paginated) and `POST /api/v1/moderation/listings/{id}/approve`.
- `GET /api/v1/auth/me` returns the signed-in account as `UserProfileDto` (user id, email, display
  name, role, created at). `PUT /api/v1/auth/me` updates only the display name, trimmed and validated
  with `AccountRules` (required, at most 120 characters). Both endpoints require `[Authorize]`:
  missing/invalid tokens return 401, an invalid name returns 400 `invalid_display_name`, and a token
  whose user row no longer exists returns 404 `profile_not_found`.

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
