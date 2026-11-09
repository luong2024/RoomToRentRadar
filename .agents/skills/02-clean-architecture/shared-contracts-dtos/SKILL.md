---
name: shared-contracts-dtos
description: Design specifications for shared DTOs exchanged between .NET MAUI Client and ASP.NET Core REST API.
---

# Shared Contracts & Data Transfer Objects (DTOs)

Prevents synchronization drift between mobile client and backend teams. Both MAUI and Web API consume this identical set of immutable Record types.

## 1. DTO Design Principles & Naming Conventions
- Use C# `public record` types to guarantee immutability and structural equality.
- Follow consistent naming suffixes:
  - **`*Dto`**: Data models returned to UI / API responses (e.g., `RoomCardDto`, `RoomDetailDto`, `GeoPointDto`).
  - **`*Request`**: Query/Command filter or payload inputs (e.g., `RoomFilterRequest`, `SpatialSearchRequest`).
  - **`PagedResult<T>`**: Standard pagination wrapper for list responses.
- Keep DTOs free of business logic.
- Ensure seamless JSON serialization and deserialization across HTTP and SQLite cache boundaries.

## 2. Reference DTO Templates for Sprint 1 (Examples)

> **Note:** The records below serve as the baseline reference templates for Sprint 1 stories. Any additions or modifications for a specific User Story must be agreed upon in the Mini-Spec and follow `.agents/GUARDRAILS.md` (Trigger #3).

```csharp
namespace RoomToRentRadar.Contracts;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

// US01: Room listing summary card
public record RoomCardDto(
    string Id,
    string Title,
    decimal MonthlyRent,
    double AreaSquareMeters,
    double DistanceToCampusKm,
    bool IsVerified,
    string ThumbnailUrl,
    string AddressSummary
);

// US02: Comprehensive room details
public record RoomDetailDto(
    string Id,
    string Title,
    string Description,
    decimal MonthlyRent,
    decimal DepositAmount,
    double AreaSquareMeters,
    string RoomType,
    IReadOnlyList<string> Amenities,
    string FullAddress,
    string LandlordName,
    string? LandlordPhoneNumber, // Only visible to signed-in users (AC4)
    IReadOnlyList<string> PhotoUrls,
    string? Panorama360Url,
    IReadOnlyList<string> HouseRules,
    DateTime LastUpdatedAt
);

// US04: Search filter request
public record RoomFilterRequest(
    decimal? MinPrice,
    decimal? MaxPrice,
    double? MinArea,
    double? MaxArea,
    string? RoomType,
    double? MaxDistanceKm,
    int PageNumber = 1,
    int PageSize = 20
);

// US05: Polygon-drawn map search region
public record GeoPointDto(double Latitude, double Longitude);

public record SpatialSearchRequest(
    IReadOnlyList<GeoPointDto> PolygonPoints,
    string SortBy = "Price" // "Price" or "Distance"
);
```
