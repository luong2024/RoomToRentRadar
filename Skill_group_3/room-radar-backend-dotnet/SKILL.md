## name: room-radar-backend-dotnet
description: Architect, implement, and test ASP.NET Core MVC REST APIs and Azure Cosmos DB data models for RoomToRentRadar. Use when building backend tasks such as listing pagination (T01.1), room details (T02.1), photo upload pipeline (T03.1), multi-filter search (T04.1), GeoJSON polygon spatial queries (T05.2), favourites sync (T06.1), saved search alert rules (T07.1), landlord publishing (T08.2), routing service (T09.1), house rules versioning (T11.1), report moderation (T12.1), or Sprint 2-4 backend features.
allowed-tools: default_api

# Room Radar Backend (.NET Core MVC & Azure Cosmos DB)

Build, optimize, and test the **ASP.NET Core MVC REST API** and **Azure Cosmos DB** persistence layer for the **RoomToRentRadar** student accommodation platform at ICTU.

## When to Use

* Implementing any backend API task in Sprint 1 (`T01.1`, `T02.1`, `T03.1`, `T04.1`, `T05.2`, `T06.1`, `T07.1`, `T08.2`, `T09.1`, `T11.1`, `T12.1`) or Sprints 2-4 (amenities, security score, deposit payment, roommate matching, odd jobs, rideshare)

* Designing Azure Cosmos DB containers, partition keys, composite indexes, or GeoJSON spatial indexes

* Implementing background workers for saved-search alerts (`US07`) or image compression pipelines (`US03`, `US08`)

* Writing xUnit tests and performance benchmarks for API endpoints (`T04.3`, `T08.3`)

## Architecture & Cost Guardrails

1. **Solution Structure**:

   * `RoomToRentRadar.Api`: ASP.NET Core Controllers, Auth middleware, Swagger/OpenAPI configuration

   * `RoomToRentRadar.Core`: Domain entities, DTOs, Interfaces, Validation rules (FluentValidation)

   * `RoomToRentRadar.Infrastructure`: Azure Cosmos DB repositories, Blob/Media storage service, Map/Routing adapters, Push notification service

   * `RoomToRentRadar.Tests`: xUnit unit and integration tests

2. **Cloud Cost & Quota Control (Risks #3 & #4)**:

   * Use the **Azure Cosmos DB Local Emulator** for all daily development and unit/integration testing; reserve the cloud Cosmos DB instance (budget: 6M VND / 12 weeks) for Staging and Pilot UAT.

   * Always project only required fields in list queries (`SELECT c.id, c.title, c.rent, c.area, c.location, c.isVerified`) instead of `SELECT *` to minimize Request Units (RUs).

   * Wrap Google Maps routing/geocoding behind an `IRoutingService` adapter with a 24-hour cache and an automatic Haversine straight-line distance fallback (`US09 AC3`).

## Steps

### Step 1: Model Cosmos DB Containers and Spatial Indexes

Consult `references/cosmos-db-and-api-contracts.md` for exact document schemas and partition keys:

* **`Listings` container** (Partition Key: `/campusId`):

  * Store coordinates as standard GeoJSON `Point` (`{"type": "Point", "coordinates": [longitude, latitude]}`) so Cosmos DB native spatial functions (`ST_DISTANCE`, `ST_WITHIN`) work out of the box.

  * Embed `houseRules` with a `version` integer and `updatedAt` timestamp (`US11 AC3`).

* **`UsersInteractions` container** (Partition Key: `/userId`):

  * Store `Favourites` (`US06`), `SavedSearches` with alert counters (`US07`), and user profile settings (selected ICTU campus).

* **`ModerationReports` container** (Partition Key: `/listingId`):

  * Store student listing reports (`US12`) and moderation audit logs (`US08`).

### Step 2: Implement Sprint 1 API Endpoints to Match Acceptance Criteria

Follow these strict domain rules for each endpoint:

1. **Listings & Filtering (`US01 T01.1`, `US04 T04.1`)**:

   * Enforce `c.status == "Available" AND c.isVerified == true` on all public student queries (`US01 AC4`).

   * Default sort by `ST_DISTANCE(c.location, @campusPoint)` ascending (`US01 AC1`).

   * Support continuation-token or offset/limit pagination and ensure response time is **<= 3 seconds for 500 listings** (`US01 AC3`) and **<= 2 seconds for combined filters** (`US04 AC2`).

2. **Room Details & Privacy Masking (`US02 T02.1`, `US11 T11.1`)**:

   * Check `User.Identity.IsAuthenticated`. If `false`, mask `landlordPhone` (e.g., `"098*** (Sign in to view)"`) to satisfy `US02 AC4`.

   * If a requested listing has `status != "Available"`, return HTTP `200 OK` with `isAvailable: false`, an explanatory message, and top 3 similar available rooms in the same `campusId` and price range (`US02 AC3`).

3. **Spatial Polygon Search (`US05 T05.2`)**:

   * Accept a closed GeoJSON `Polygon` coordinate ring (first and last point identical, counter-clockwise winding order).

   * Query Cosmos DB using `WHERE ST_WITHIN(c.location, @drawnPolygon)` and sort by `rent` or `distance` (`US05 AC1, AC2`).

4. **Photo Pipeline & Landlord Publishing (`US03 T03.1`, `US08 T08.2`)**:

   * Validate file extension (`.jpg`, `.jpeg`, `.png`) and size (`<= 10 MB` per file) (`US08 AC4`).

   * Require at least 3 photos on listing creation (`US08 AC1`) and support >= 8 photos plus equirectangular 360-degree panoramas (`US03 AC1, AC2`).

   * Force initial status of newly published listings to `"PendingReview"` (`US08 AC2`).

5. **Saved Searches & Rate-Limited Alert Worker (`US07 T07.1`)**:

   * Run a background `IHostedService` or Cosmos DB Change Feed processor when a listing transitions to `"Verified"`.

   * Match against active `SavedSearch` rules within 15 minutes (`US07 AC2`).

   * Enforce rate limiting: check `alertsSentToday < 3` before dispatching push notifications (`US07 AC4`).

### Step 3: Write xUnit Tests and Verify Performance Budgets

Before opening a PR, run the test suite and verify all AC edge cases:

```
dotnet test --configuration Release --logger "console;verbosity=detailed"

```

Ensure unit tests cover:

* Unauthenticated vs. authenticated landlord phone masking (`US02 AC4`)

* Unavailable listing fallback recommendations (`US02 AC3`)

* Image > 10MB and < 3 photos rejection (`US08 AC1, AC4`)

* Daily alert cap at 3 notifications per saved search (`US07 AC4`)

* Routing API failure graceful fallback to straight-line distance (`US09 AC3`)

## Gotchas

* **GeoJSON Coordinate Order**: GeoJSON and Azure Cosmos DB strictly use `[longitude, latitude]` (e.g., `[105.8066, 21.5850]` for ICTU Thai Nguyen), whereas Google Maps UI often uses `(latitude, longitude)`. Swapping them will cause `ST_DISTANCE` and `ST_WITHIN` to return 0 results.

* **Polygon Ring Closure**: Cosmos DB `ST_WITHIN` throws a query syntax error if the first and last coordinate pair of a drawn polygon do not match exactly. Always close the polygon array in the API controller before querying.

---

## Bundled File: 

# Azure Cosmos DB Schemas & REST API Contracts (RoomToRentRadar)

## 1. Cosmos DB Container Design & Indexing Policy

### Container 1: `Listings` (Partition Key: `/campusId`)

```json
{
  "id": "room-2026-001",
  "campusId": "ictu-main",
  "title": "Phòng khép kín ban công thoáng cách ICTU 400m",
  "description": "Phòng mới xây, an ninh vân tay, giờ giấc tự do...",
  "status": "Available",
  "isVerified": true,
  "moderationStatus": "Approved",
  "rent": 1800000,
  "deposit": 1800000,
  "area": 22.5,
  "roomType": "Studio",
  "address": "Đường Z115, Quyết Thắng, TP. Thái Nguyên",
  "location": {
    "type": "Point",
    "coordinates": [105.8066, 21.5850]
  },
  "amenities": ["Wifi", "WaterHeater", "AirConditioner", "PrivateWC", "FingerprintLock"],
  "securityScore": 4.5,
  "photos": [
    { "url": "https://.../p1.jpg", "is360": false, "order": 1 },
    { "url": "https://.../p360.jpg", "is360": true, "order": 2 }
  ],
  "houseRules": {
    "version": 2,
    "updatedAt": "2026-11-23T08:00:00Z",
    "quietHours": "23:00 - 05:30",
    "guestPolicy": "Khách về trước 22:30, qua đêm báo trước chủ nhà",
    "cookingRules": "Được nấu ăn bằng bếp từ, không dùng bếp gas mini",
    "parkingRules": "Để xe đúng vạch tầng 1, khóa cổ xe"
  },
  "landlord": {
    "id": "usr-landlord-09",
    "name": "Cô Lan",
    "phone": "0987654321"
  },
  "updatedAt": "2026-11-23T09:00:00Z"
}
```

**Spatial Indexing Policy (`Listings`)**:
```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [{ "path": "/*" }],
  "spatialIndexes": [
    {
      "path": "/location/?",
      "types": ["Point", "Polygon"]
    }
  ]
}
```

## 2. Core REST API Endpoints (Sprint 1)

| User Story & Task | HTTP Method & Route | Query / Body Parameters | Response & AC Rules |
| :--- | :--- | :--- | :--- |
| **US01 / T01.1** | `GET /api/v1/listings` | `?campusId=ictu-main&pageSize=20&continuationToken=...&sortBy=distance` | Returns `PagedResult<ListingCardDto>`. Filters `status='Available' AND isVerified=true`. Default sorted by `ST_DISTANCE`. |
| **US02 / T02.1** | `GET /api/v1/listings/{id}` | Route `id` (Optional Bearer JWT) | Returns `ListingDetailDto`. Masks `landlord.phone` if unauthenticated. If unavailable, includes `similarRooms` array. |
| **US03 / T03.1** | `POST /api/v1/listings/{id}/photos` | `multipart/form-data` (`files`, `is360` flags) | Validates JPG/PNG `<= 10MB`, compresses thumbnail, uploads to storage, updates `photos` array. |
| **US04 / T04.1** | `GET /api/v1/listings/search` | `?campusId=ictu-main&minRent=1000000&maxRent=2500000&minArea=15&maxArea=30&roomType=Studio&maxDistanceMeters=2000` | Returns `SearchResponseDto` with `totalCount`, `activeFilters`, and `items` within 2 seconds. |
| **US05 / T05.2** | `POST /api/v1/listings/spatial-search` | Body: `{ "polygon": [[lng1,lat1], [lng2,lat2], ..., [lng1,lat1]], "sortBy": "price" }` | Auto-closes polygon ring if needed; runs `ST_WITHIN(c.location, @polygon)` and returns pin preview cards. |
| **US06 / T06.1** | `POST /api/v1/favourites/toggle` | Body: `{ "listingId": "...", "action": "Add|Remove" }` | Syncs favourites across user devices; returns current `rent` and `status` for favourites list. |
| **US07 / T07.1** | `POST /api/v1/saved-searches` | Body: `{ "name": "Khu Z115 < 2tr", "filters": {...}, "polygon": [...], "alertsEnabled": true }` | Stores rule; background worker sends max 3 push alerts/day within 15m of new verified match. |
| **US08 / T08.2** | `POST /api/v1/landlord/listings` | Body: `CreateListingRequest` (>= 3 photos required) | Sets `status = "PendingReview"` and `isVerified = false` until moderator approves. |
| **US09 / T09.1** | `GET /api/v1/listings/{id}/commute` | `?campusId=ictu-main` | Returns motorbike, bus, and walking travel times; falls back to straight-line Haversine distance if Maps API fails. |
| **US11 / T11.1** | `PUT /api/v1/listings/{id}/house-rules` | Body: `UpdateHouseRulesDto` | Increments `houseRules.version` and appends previous snapshot to history. |
| **US12 / T12.1** | `POST /api/v1/listings/{id}/reports` | Body: `{ "reason": "FakePhoto|WrongPrice|RentedOut", "note": "..." }` | Enqueues report into `ModerationReports` container and returns confirmation ID. |
