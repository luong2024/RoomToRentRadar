---
name: ports-and-adapters
description: Guide for designing Driving Input Ports (UseCases) and Driven Output Ports (Repositories, External Services).
---

# Ports & Adapters Design Pattern

Standardizes naming conventions and structural patterns for Ports (interfaces) and Adapters (implementations) within `RoomToRentRadar`.

> **Convention Note:** All code snippets below are reference examples illustrating naming conventions (`I*UseCase`, `I*Port`, `*Adapter`). Concrete type names and method signatures for each feature are specified per User Story in the Mini-Spec.

## 1. Differentiating the Two Port Categories

### 1.1. Driving / Input Ports (`I*UseCase`)
- Interfaces through which the outside world (PageModels, API Controllers) drives application use cases.
- Typically represented by Use Case interfaces (`I*UseCase`) implemented by `*UseCase` classes:
  ```csharp
  // Example Input Port pattern
  public interface ISearchRoomsUseCase
  {
      Task<PagedResult<RoomCardDto>> ExecuteAsync(RoomFilterRequest request, CancellationToken ct = default);
  }
  ```

### 1.2. Driven / Output Ports (`I*Port`)
- Interfaces through which the Core layer asks external infrastructure (Databases, Networks, Hardware sensors) to persist or deliver data:
  ```csharp
  // Example Output Port pattern
  public interface IRoomApiClientPort
  {
      Task<PagedResult<RoomCardDto>> SearchRoomsAsync(RoomFilterRequest request, CancellationToken ct = default);
      Task<RoomDetailDto?> GetByIdAsync(string id, CancellationToken ct = default);
  }
  ```

## 2. Adapter Naming & Implementation Rules (`*Adapter`)
- **Adapter names MUST end with `Adapter` and prefix the concrete technology:**
  - `SqliteOfflineCacheAdapter` implements `ILocalCachePort`.
  - `HttpRoomApiAdapter` implements `IRoomApiClientPort`.
  - `MockRoomApiAdapter` implements `IRoomApiClientPort`.
- When offline testing or mocking is required, swap adapters inside Dependency Injection registration without modifying a single line of business logic in Use Cases.
