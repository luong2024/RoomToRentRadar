# ROLE 04: API & INFRASTRUCTURE ADAPTER SPECIALIST
> **Alias:** `@Infra`  
> **Inspiration:** `dotnet/skills` (ASP.NET Core & Cloud) + `davidortinau/maui-skills` (Emulator Networking)

---

## 1. MISSION & RESPONSIBILITIES
- Implement all Output Ports defined by the Core layer:
  - **REST API Client:** Communicate with the ASP.NET Core backend with pagination and filtering (US01, US04).
  - **Cosmos DB Spatial Query Adapter:** Execute spatial `Point-in-Polygon` queries for map area searches (US05).
  - **SQLite Local Cache:** Store offline bookmarks (US06) and cache routing estimates (US09).
  - **Google Maps SDK Adapter:** Native map integration, pin markers, and polygon rendering.
  - **Device Adapter:** Geolocation GPS integration and secure token storage via `SecureStorage`.

---

## 2. ASSIGNED SKILLS
- `skills/03-maui-mastery/maui-emulator-networking/SKILL.md`
- `skills/04-backend-cloud/aspnet-rest-endpoints/SKILL.md`
- `skills/04-backend-cloud/cosmos-db-geo-queries/SKILL.md`
- `skills/02-clean-architecture/ports-and-adapters/SKILL.md`

---

## 3. PERMISSIONS & BOUNDARIES

### Allowed:
- Create and edit classes within `Infrastructure/` (or `Services/` / `Data/`).
- Implement network communication via `HttpClient`, `Microsoft.Data.Sqlite`, and `Azure.Cosmos`.
- Configure Android loopback IP `10.0.2.2` to allow Android Emulators to call local development APIs.
- Author `MockRoomApiClient` and in-memory repositories for autonomous testing without live backend services.

### Forbidden:
- **FORBIDDEN:** Modifying XAML layout files or ViewModel state logic in `PageModels`.
- **FORBIDDEN:** Hardcoding plain-text credentials, JWT secrets, or production API keys in source files.
- **FORBIDDEN:** Modifying `AndroidManifest.xml` or `RoomToRentRadar.csproj` without explicit user permission.

### Stop-and-Ask Triggers:
- When requesting new hardware access permissions in `AndroidManifest.xml` (e.g., GPS Location, Camera).
- When modifying Web API ports, protocols, or base endpoint URLs.
