---
name: room-radar-backend-dotnet
description: Architect, implement, and test ASP.NET Core MVC (.NET 10 LTS) REST APIs, optional Razor Web Portal views, and Azure Cosmos DB data models for RoomToRentRadar. Assigned exclusively to Backend Developers (Nguyen Minh Hieu & Be Van Duy). Use when building backend tasks such as listing pagination (T01.1), room details (T02.1), photo upload pipeline (T03.1), multi-filter search (T04.1), filter performance tests (T04.3), GeoJSON polygon spatial queries (T05.2), favourites sync (T06.1), saved search & push alert rules (T07.1, T07.2), landlord publishing & moderation (T08.2, T08.3), routing service (T09.1), house rules API (T11.1), or report moderation (T12.1).
allowed-tools: default_api
---
# Room Radar Backend (.NET 10 LTS ASP.NET Core MVC & Azure Cosmos DB)

Build, optimize, and test the **ASP.NET Core MVC REST API + Web Portal (`net10.0`)** and **Azure Cosmos DB** persistence layer for **RoomToRentRadar** at ICTU.

## When to Use

- Implementing any Backend task assigned to **Nguyễn Minh Hiếu (Backend Lead)** (`T01.1`, `T03.1`, `T04.3`, `T06.1`, `T07.2`, `T08.3`, `T11.1`) or **Bế Văn Duy (Backend Developer)** (`T02.1`, `T04.1`, `T05.2`, `T07.1`, `T08.2`, `T09.1`, `T12.1`)
- Designing `RoomToRentRadar.Shared` DTOs (`net10.0`) shared between Backend API and `.NET MAUI` mobile app
- Building optional **ASP.NET Core MVC Razor Views (`.cshtml`)** for Landlord listing management (`US08`) or Admin report moderation (`US12`) under Option 1 architecture
- Writing Azure Cosmos DB spatial queries (`ST_DISTANCE`, `ST_WITHIN`) and xUnit performance tests

## Standardized .NET 10 LTS Architecture & Cost Guardrails

1. **Framework & SDK Baseline**:
   - Target Framework: **`net10.0`** (.NET 10.0 LTS, C# 14, Visual Studio 2026).
   - Pin SDK version at solution root via `global.json` (`"version": "10.0.100"`, `"rollForward": "latestFeature"`).
   - Core NuGet Packages: `Microsoft.Azure.Cosmos` (v3), `Swashbuckle.AspNetCore` / `Microsoft.AspNetCore.OpenApi`, `FluentValidation.AspNetCore`, `SixLabors.ImageSharp` (for image compression <= 10MB).
2. **Solution Structure (Option 1 - Shared DTOs + API/MVC Web + MAUI)**:
   - `RoomToRentRadar.Shared` (`net10.0`): Pure C# DTOs, Request/Response records, and Enums shared directly with `RoomToRentRadar.Maui`.
   - `RoomToRentRadar.Api` (`net10.0`):
     - `Controllers/Api/`: REST API controllers (`[ApiController]`, `[Route("api/v1/[controller]")]`) consumed by the `.NET MAUI` mobile app.
     - `Controllers/Web/` & `Views/`: Standard ASP.NET Core MVC controllers and Razor Views (`.cshtml`) if a web browser interface is needed for Landlords (`US08`) or Moderators (`US12`).
   - `RoomToRentRadar.Tests` (`net10.0`): xUnit unit/integration tests (`Moq`, `FluentAssertions`).
3. **Cloud Cost & Quota Control (Risks #3 & #4)**:
   - Use the **Azure Cosmos DB Local Emulator** (`https://localhost:8081`) for all daily development and unit testing; reserve cloud Cosmos DB (budget: 6M VND / 12 weeks) for Staging and Pilot UAT.
   - Project only required fields in list queries (`SELECT c.id, c.title, c.rent, c.area, c.location, c.isVerified`) instead of `SELECT *` to minimize Request Units (RUs).
   - Wrap Google Maps routing/geocoding behind an `IRoutingService` adapter with a 24-hour cache and an automatic Haversine straight-line distance fallback (`US09 AC3`).

## Steps

### Step 1: Model Cosmos DB Containers and Spatial Indexes

Consult `references/cosmos-db-and-api-contracts.md` for exact document schemas and partition keys:

- **`Listings` container** (Partition Key: `/campusId`):
  - Store coordinates as standard GeoJSON `Point` (`{"type": "Point", "coordinates": [longitude, latitude]}`).
  - Embed `houseRules` with a `version` integer and `updatedAt` timestamp (`US11 AC3`).
- **`UsersInteractions` container** (Partition Key: `/userId`):
  - Store `Favourites` (`US06`), `SavedSearches` with daily alert counters (`US07`), and user profile settings.
- **`ModerationReports` container** (Partition Key: `/listingId`):
  - Store student listing reports (`US12`) and moderation audit logs (`US08`).

### Step 2: Implement Sprint 1 API Endpoints to Match Acceptance Criteria

1. **Listings & Filtering (`US01 T01.1` Hiếu, `US04 T04.1` Duy, `T04.3` Hiếu)**:
   - Enforce `c.status == "Available" AND c.isVerified == true` on all public student queries (`US01 AC4`).
   - Default sort by `ST_DISTANCE(c.location, @campusPoint)` ascending (`US01 AC1`).
   - Ensure response time is **<= 3 seconds for 500 listings** (`US01 AC3`) and **<= 2 seconds for combined filters** (`US04 AC2`).
2. **Room Details & Privacy Masking (`US02 T02.1` Duy, `US11 T11.1` Hiếu)**:
   - Check `User.Identity.IsAuthenticated`. If `false`, mask `landlordPhone` (`"098*** (Sign in to view)"`) to satisfy `US02 AC4`.
   - If a requested listing has `status != "Available"`, return HTTP `200 OK` with `isAvailable: false`, an explanatory message, and top 3 similar available rooms (`US02 AC3`).
3. **Spatial Polygon Search (`US05 T05.2` Duy)**:
   - Accept a closed GeoJSON `Polygon` coordinate ring (auto-close first and last point if needed).
   - Query Cosmos DB using `WHERE ST_WITHIN(c.location, @drawnPolygon)` and sort by `rent` or `distance` (`US05 AC1, AC2`).
4. **Photo Pipeline & Landlord Publishing (`US03 T03.1` Hiếu, `US08 T08.2` Duy, `T08.3` Hiếu)**:
   - Validate file extension (`.jpg`, `.jpeg`, `.png`) and size (`<= 10 MB` per file) (`US08 AC4`).
   - Require at least 3 photos on listing creation (`US08 AC1`) and support >= 8 photos plus equirectangular 360-degree panoramas (`US03 AC1, AC2`).
   - Force initial status of newly published listings to `"PendingReview"` (`US08 AC2`).
5. **Saved Searches & Rate-Limited Push Alert Worker (`US07 T07.1` Duy, `T07.2` Hiếu)**:
   - Run a background `IHostedService` when a listing transitions to `"Verified"` to dispatch push notifications within 15 minutes (`US07 AC2`).
   - Enforce rate limiting: check `alertsSentToday < 3` per saved search per day (`US07 AC4`).
6. **Routing Service & Report Queue (`US09 T09.1` Duy, `US12 T12.1` Duy)**:
   - Calculate motorbike, bus, and walking commute times from selected ICTU campus; fallback to Haversine straight-line distance if routing API is unavailable (`US09 AC1-AC3`).

### Step 3: Write xUnit Tests and Verify Performance Budgets

```bash
dotnet test --configuration Release --logger "console;verbosity=detailed"
```

## Gotchas

- **GeoJSON Coordinate Order**: GeoJSON and Azure Cosmos DB strictly use `[longitude, latitude]` (e.g., `[105.8066, 21.5850]` for ICTU Thai Nguyen), whereas Google Maps UI uses `(latitude, longitude)`.
- **Android Emulator Loopback**: Configure `launchSettings.json` to listen on `http://0.0.0.0:5000` so the Android Emulator (`http://10.0.2.2:5000`) can reach your local ASP.NET Core API.
