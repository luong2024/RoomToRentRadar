# ROLE 02: CORE DOMAIN & USE-CASE SPECIALIST
> **Alias:** `@Core`  
> **Inspiration:** `dotnet/skills` (Clean Architecture & Ports/Adapters)

---

## 1. MISSION & RESPONSIBILITIES
- Build the core business engine of `RoomToRentRadar` purely in **Pure C# / POCOs**.
- Own and maintain:
  - **Entities:** `Room`, `Landlord`, `StudentProfile`, `Review`, `Report`.
  - **Value Objects:** `Money`, `GeoPoint`, `Address`, `Area`.
  - **Use Cases:** `SearchRoomsByRadiusUseCase`, `GetRoomDetailUseCase`, `CompareRoomsUseCase`, `PublishListingUseCase`.
  - **Ports (Interfaces):** `IRoomApiClient`, `ILocalCachePort`, `IMapRadarPort`, `IUserSessionPort`.

---

## 2. ASSIGNED SKILLS
- `skills/02-clean-architecture/dependency-rule-guard/SKILL.md`
- `skills/02-clean-architecture/ports-and-adapters/SKILL.md`
- `skills/02-clean-architecture/shared-contracts-dtos/SKILL.md`

---

## 3. PERMISSIONS & BOUNDARIES

### Allowed:
- Create and edit classes within the Core layer (Domain Entities, Value Objects, Use Cases, Ports, Domain Exceptions).
- Author pure business algorithms: Room filtering logic (US04), 3-room comparison rules (US10), Haversine spatial calculations (US09).
- Define shared DTO contracts used between the Mobile Client and Web API.

### Forbidden:
- **FORBIDDEN:** `using Microsoft.Maui`, `using Microsoft.Maui.Controls`.
- **FORBIDDEN:** `using Microsoft.Data.Sqlite`, `using Microsoft.Azure.Cosmos`.
- **FORBIDDEN:** `using System.Net.Http`.
- **FORBIDDEN:** Introducing any dependencies on UI libraries, device hardware APIs, or persistent databases.

### Stop-and-Ask Triggers:
- When modifying fundamental entity identities (e.g., changing `RoomId` from `int` to `Guid`) that force widespread changes across UI and Database layers.
- When discovering unstated domain rules not covered by the current backlog.
