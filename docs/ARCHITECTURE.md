# RoomToRentRadar — Architecture Guide (Group 1)

Practical architecture and structure rules for the group-1 codebase. This document describes what the
repository actually uses today. Update it when an architecture decision changes.

## 1. Architecture decisions

| Area | Decision |
| --- | --- |
| Mobile app | Feature-based folders + MVVM (CommunityToolkit.Mvvm) |
| Backend | ASP.NET Core Web API with MVC controllers and feature slices |
| Database | EF Core 10 + SQLite (single instance, WAL enabled) |
| Contracts | `RoomToRentRadar.Shared` — DTOs shared by API and mobile app |
| Communication | REST/JSON, cursor pagination |
| DI | Microsoft.Extensions.DependencyInjection everywhere |
| Tests | NUnit 4 for unit, integration and UI tests |

Hexagonal architecture is not used in group-1. Keep the existing feature-slice API; do not introduce
layer projects (`Domain`, `Application`, `Adapters`, `Bootstrap`) without an explicit decision.

## 2. Repository layout

```text
RoomToRentRadar.slnx
RoomToRentRadar/                  .NET MAUI app (Android, iOS, Mac Catalyst, Windows)
RoomToRentRadar.Api/              ASP.NET Core Web API
RoomToRentRadar.Api.Tests/        API unit + integration tests
RoomToRentRadar.Shared/           DTOs shared by API and app
RoomToRentRadarUnitTests/         Mobile ViewModel unit tests
RoomToRentRadarUIAppiumTests/     Appium UI tests
docs/                             Architecture and process documentation
```

Do not move projects into `src/` / `tests/` without updating the solution and CI.

## 3. Mobile application (`RoomToRentRadar/`)

### 3.1 Folder structure

```text
RoomToRentRadar/
├── Core/
│   ├── Constants/                Routes, API endpoints, base URLs
│   ├── Interfaces/               Abstractions consumed by features
│   └── Extensions/
├── Features/
│   └── <Feature>/
│       ├── Views/                XAML pages + code-behind
│       ├── ViewModels/           Presentation logic
│       ├── Models/               Feature-only models (not API DTOs)
│       └── Services/             Feature services
├── Infrastructure/
│   ├── Api/                      HTTP client and API wrapper
│   ├── DependencyInjection/      ServiceCollection extension methods
│   └── ...                       Storage, notifications, platform services
├── Shared/                       Reusable controls, converters, behaviors
├── Resources/                    Styles, images, fonts
├── Platforms/                    Platform-specific code
├── App.xaml, AppShell.xaml, MauiProgram.cs
```

Create a folder only when it contains code. Do not add empty layers.

### 3.2 MVVM rules

- ViewModels derive from `ObservableObject` and use `[ObservableProperty]` / `[RelayCommand]`.
- ViewModels must not call static MAUI APIs (`Shell.Current`, `Application.Current`,
  `SecureStorage.Default`, `Connectivity.Current`). Inject an interface instead.
- Pages receive their ViewModel through constructor injection and assign `BindingContext`.
- Every page sets `x:DataType`; every `DataTemplate` sets its own `x:DataType`.
  `XC0022` and `XC0025` are build errors.
- Binding failures and UI state belong to the ViewModel; code-behind only wires lifecycle events.

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
- Routes are declared in `AppRoutes` (`Core/Constants`) and registered in the `AppShell` constructor
  when they are not root shell content.
- Pass parameters to the ViewModel with `IQueryAttributable`, not to the page.

### 3.5 API communication

- All HTTP calls go through `Infrastructure/Api` — features never create `HttpClient` themselves.
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

## 4. Backend (`RoomToRentRadar.Api/`)

### 4.1 Structure

```text
RoomToRentRadar.Api/
├── Controllers/          Thin controllers, HTTP concerns only
├── Features/<Feature>/   Query/service logic, validation, feature-specific helpers
├── Data/                 DbContext, configurations, migrations, seeding
├── Domain/               EF Core entities
└── Properties/
```

- Add new behavior as a feature folder. Do not create generic `Services/` or `Repositories/` buckets.
- `Program.cs` stays a composition root; extract registration into extension methods when it grows.
- Never return EF entities from endpoints. Use DTOs from `RoomToRentRadar.Shared`.

### 4.2 API rules

- MVC controllers with `[ApiController]` and attribute routing. Do not mix with Minimal APIs per feature.
- Errors use RFC 9457 `ProblemDetails` with a stable `code` extension.
- Validation happens at the boundary; invalid input produces 400 with `ProblemDetails`.
- OpenAPI is generated by the built-in .NET 10 support; Swagger UI is only a viewer.
- Schema changes use EF Core migrations. Never use `EnsureCreated`.
- Development startup applies migrations and seeds sample data; this is a documented exception.

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
