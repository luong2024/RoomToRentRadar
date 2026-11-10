---
name: aspnet-rest-endpoints
description: Design principles for ASP.NET Core REST API controllers implementing pagination, sorting, and filtering.
---

# ASP.NET Core Clean REST API Endpoints

Derived from `dotnet/skills/aspnet-core`. Guides the implementation of RESTful endpoints serving Product Backlog User Stories (e.g., US01, US04, US08, US13).

## 1. REST Controller Template for Browsing & Filtering (Example for US01, US04)

> **Note:** Controller, UseCase (`I*UseCase`), and DTO (`*Request`, `*Dto`) names below illustrate the standard pattern. Actual names for each endpoint are aligned with the User Story Mini-Spec.

```csharp
[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly ISearchRoomsUseCase _searchRoomsUseCase;
    private readonly IGetRoomDetailUseCase _getRoomDetailUseCase;

    public RoomsController(
        ISearchRoomsUseCase searchRoomsUseCase,
        IGetRoomDetailUseCase getRoomDetailUseCase)
    {
        _searchRoomsUseCase = searchRoomsUseCase;
        _getRoomDetailUseCase = getRoomDetailUseCase;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<RoomCardDto>>> GetRooms(
        [FromQuery] RoomFilterRequest request,
        CancellationToken ct)
    {
        // Enforce maximum page size to prevent mobile bandwidth exhaustion
        var clampedPageSize = Math.Clamp(request.PageSize, 1, 50);
        var safeRequest = request with { PageSize = clampedPageSize };

        var result = await _searchRoomsUseCase.ExecuteAsync(safeRequest, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoomDetailDto>> GetRoomById(string id, CancellationToken ct)
    {
        var room = await _getRoomDetailUseCase.ExecuteAsync(id, ct);
        if (room == null)
        {
            return NotFound(new { Message = $"Room listing with ID '{id}' was not found." });
        }
        return Ok(room);
    }
}
```

## 2. Response & Execution Standards
- Propagate `CancellationToken` from controller actions down through all data access operations so orphaned requests abort when users navigate away.
- Use explicit HTTP status codes:
  - `200 OK`: Query completed successfully.
  - `201 Created`: Room listing published successfully (US08).
  - `400 Bad Request`: Validation failure (e.g., MinPrice exceeds MaxPrice).
  - `404 Not Found`: Listing does not exist or has been unpublished.
