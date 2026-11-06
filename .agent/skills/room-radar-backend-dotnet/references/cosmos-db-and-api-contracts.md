# .NET 10 Solution Configuration, Cosmos DB Schemas & REST API Contracts

## 1. Root `global.json` (.NET 10 LTS Pinning)

Place at the root of the `Group-3` branch so all team members compile with .NET 10 LTS:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

## 2. Cosmos DB `Listings` Document Schema (Partition Key: `/campusId`)

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

## 3. Core REST API Endpoints & Pure Backend Ownership (Sprint 1)

| User Story & Task | Backend Assignee | HTTP Method & Route | Query / Body Parameters | Response & AC Rules |
| :--- | :--- | :--- | :--- | :--- |
| **US01 / T01.1** | Nguyễn Minh Hiếu | `GET /api/v1/listings` | `?campusId=ictu-main&pageSize=20&continuationToken=...&sortBy=distance` | Returns `PagedResult<ListingCardDto>`. Filters `status='Available' AND isVerified=true`. Default sorted by `ST_DISTANCE`. |
| **US02 / T02.1** | Bế Văn Duy | `GET /api/v1/listings/{id}` | Route `id` (Optional Bearer JWT) | Returns `ListingDetailDto`. Masks `landlord.phone` if unauthenticated. If unavailable, includes `similarRooms` array. |
| **US03 / T03.1** | Nguyễn Minh Hiếu | `POST /api/v1/listings/{id}/photos` | `multipart/form-data` (`files`, `is360` flags) | Validates JPG/PNG `<= 10MB`, compresses thumbnail, uploads to storage, updates `photos` array. |
| **US04 / T04.1** | Bế Văn Duy | `GET /api/v1/listings/search` | `?campusId=ictu-main&minRent=1000000&maxRent=2500000&minArea=15&maxArea=30&roomType=Studio&maxDistanceMeters=2000` | Returns `SearchResponseDto` with `totalCount`, `activeFilters`, and `items` within 2 seconds. |
| **US05 / T05.2** | Bế Văn Duy | `POST /api/v1/listings/spatial-search` | Body: `{ "polygon": [[lng1,lat1], [lng2,lat2], ..., [lng1,lat1]], "sortBy": "price" }` | Auto-closes polygon ring if needed; runs `ST_WITHIN(c.location, @polygon)` and returns pin preview cards. |
| **US06 / T06.1** | Nguyễn Minh Hiếu | `POST /api/v1/favourites/toggle` | Body: `{ "listingId": "...", "action": "Add|Remove" }` | Syncs favourites across user devices; returns current `rent` and `status` for favourites list. |
| **US07 / T07.1, T07.2** | Duy (`T07.1`) & Hiếu (`T07.2`) | `POST /api/v1/saved-searches` | Body: `{ "name": "Khu Z115 < 2tr", "filters": {...}, "polygon": [...], "alertsEnabled": true }` | Stores rule (`T07.1`); background push worker (`T07.2`) sends max 3 alerts/day within 15m of new verified match. |
| **US08 / T08.2, T08.3** | Duy (`T08.2`) & Hiếu (`T08.3`) | `POST /api/v1/landlord/listings` | Body: `CreateListingRequest` (>= 3 photos required) | Sets `status = "PendingReview"` and `isVerified = false` until moderator approves (`T08.3`). |
| **US09 / T09.1** | Bế Văn Duy | `GET /api/v1/listings/{id}/commute` | `?campusId=ictu-main` | Returns motorbike, bus, and walking travel times; falls back to straight-line Haversine distance if Maps API fails. |
| **US11 / T11.1** | Nguyễn Minh Hiếu | `PUT /api/v1/listings/{id}/house-rules` | Body: `UpdateHouseRulesDto` | Increments `houseRules.version` and appends previous snapshot to history. |
| **US12 / T12.1** | Bế Văn Duy | `POST /api/v1/listings/{id}/reports` | Body: `{ "reason": "FakePhoto|WrongPrice|RentedOut", "note": "..." }` | Enqueues report into `ModerationReports` container and returns confirmation ID. |
