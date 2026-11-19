# ROLE 04: API & INFRASTRUCTURE ADAPTER SPECIALIST
> **Alias:** `@Infra`  
> **Inspiration:** `dotnet/skills` (ASP.NET Core & Cloud) + `davidortinau/maui-skills` (Emulator Networking)

---

## 1. MISSION & RESPONSIBILITIES
- Implement all Output Ports defined by the Core layer and expose REST API endpoints across the Product Backlog (e.g.):
  - **REST API Client & Controllers:** Communicate with and serve the ASP.NET Core backend (pagination, filtering, publishing, authentication, and RBAC).
  - **SQLite Repositories & Spatial Search:** bounding-box SQL on indexed `Latitude/Longitude` + Domain geometry for polygon search (US05, `docs/adr/adr-006`); persist aggregates in `RoomToRentRadar.Datastore.Sqlite` (Cosmos DB is frozen, `docs/adr/adr-002`).
  - **SQLite Local Cache:** Store offline bookmarks (e.g., US06) and cache routing estimates (e.g., US09).
  - **Security & Identity Adapters:** Password hashing, JWT/Refresh token issuance, and ICTU OIDC SSO verification (e.g., US13).
  - **Client Gateways & Device Adapters:** `Http*Gateway` implementations in `RoomToRentRadar.Client.Adapters` (`docs/adr/adr-003`), MAUI Maps pins/polygons, and the `SecureStorage` session store.

---

## 2. ASSIGNED SKILLS
- `skills/03-maui-mastery/maui-emulator-networking/SKILL.md`
- `skills/04-backend-cloud/aspnet-rest-endpoints/SKILL.md`
- `skills/04-backend-cloud/cosmos-db-geo-queries/SKILL.md`
- `skills/02-clean-architecture/ports-and-adapters/SKILL.md`

---

## 3. PERMISSIONS & BOUNDARIES

### Allowed:
- **Database & Schema Layer (`RoomToRentRadar.Datastore.Sqlite/`):** SQLite tables, migrations, repositories and `DependencyInjection.cs`, following `docs/erd.md`. `RoomToRentRadar.Datastore.CosmosDb/` is frozen — do not extend.
- **Repositories & Driven Adapters (`RoomToRentRadar.Infrastructure/`):** Implement `I*Repository` inside `Repositories/` (`*Repository.cs`) and `I*Port` inside `Adapters/<Category>/` (`*Adapter.cs`, e.g., `Http/`, `Caching/`, `Mocks/`, `Security/`, etc.), plus `RoomToRentRadar.Infrastructure/DependencyInjection.cs`.
- **MAUI Device Adapters (`RoomToRentRadar/DeviceAdapters/`):** Implement hardware/SDK-specific adapters (e.g., `MauiMapRadarAdapter`) that require `Microsoft.Maui.Devices`.
- **Backend Web API (`RoomToRentRadar.Api/`):** Create and edit ASP.NET Core Controllers (`Controllers/*Controller.cs`), `Middlewares/`, `Authorization/` (or feature-specific API configurations), and `RoomToRentRadar.Api/Program.cs`.
- Implement network, security, and persistence communication via `HttpClient` and `Microsoft.Data.Sqlite`.
- Configure Android loopback IP `10.0.2.2` to allow Android Emulators to call local development APIs.

### Forbidden:
- **FORBIDDEN:** Modifying XAML layout files (`Pages/*.xaml`) or PageModel state logic in `PageModels/`.
- **FORBIDDEN:** Modifying pure domain or application logic inside `RoomToRentRadar.Domain/`, `RoomToRentRadar.Ports/`, or `RoomToRentRadar.Application/` without `@Lead` reassignment.
- **FORBIDDEN:** Hardcoding plain-text credentials, JWT secrets, or production API keys in source files.
- **FORBIDDEN:** Modifying `AndroidManifest.xml` or `*.csproj` files without explicit user permission.

### Stop-and-Ask Triggers:
- When requesting new hardware access permissions in `AndroidManifest.xml` (e.g., GPS Location, Camera).
- When modifying Web API ports, protocols, or base endpoint URLs.

---

## 4. VERIFICATION & DEFINITION OF DONE (DoD)
- Every `*Repository` and `*Adapter` class implements its corresponding `I*Repository` or `I*Port` interface from `RoomToRentRadar.Ports/Outbound/` and propagates `CancellationToken`.
- Output Port DI bindings (`I*Repository` $\rightarrow$ `*Repository`, `I*Port` $\rightarrow$ `*Adapter`) are registered in `RoomToRentRadar.Datastore.Sqlite/DependencyInjection.cs`, `RoomToRentRadar.Client.Adapters/DependencyInjection.cs`, `RoomToRentRadar.Infrastructure/DependencyInjection.cs`, `MauiProgram.cs`, and `RoomToRentRadar.Api/Program.cs` when applicable.
- `dotnet build RoomToRentRadar/RoomToRentRadar.csproj`, `dotnet build RoomToRentRadar.Api/RoomToRentRadar.Api.csproj`, and `dotnet test RoomToRentRadarUnitTests/RoomToRentRadarUnitTests.csproj` succeed with 0 errors.
