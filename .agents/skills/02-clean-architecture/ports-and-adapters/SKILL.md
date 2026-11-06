---
name: ports-and-adapters
description: Guide for designing Driving Input Ports (UseCases) and Driven Output Ports (Repositories, External Services).
---

# Ports & Adapters Design Pattern

Standardizes the definition of Ports (interfaces) and Adapters (implementations) within `RoomToRentRadar`.

## 1. Differentiating the Two Port Categories

### 1.1. Driving / Input Ports
- Interfaces through which the outside world (PageModels, API Controllers) drives application use cases.
- Typically represented by Use Case or Interactor interfaces:
  ```csharp
  public interface ISearchRoomsUseCase
  {
      Task<IReadOnlyList<RoomSummaryDto>> ExecuteAsync(SearchRoomsQuery query, CancellationToken ct = default);
  }
  ```

### 1.2. Driven / Output Ports
- Interfaces through which the Core layer asks external infrastructure (Databases, Networks, Hardware sensors) to persist or deliver data:
  ```csharp
  public interface IRoomRepositoryPort
  {
      Task<IReadOnlyList<Room>> GetActiveRoomsAsync(CancellationToken ct = default);
      Task<Room?> GetByIdAsync(string id, CancellationToken ct = default);
      Task SaveAsync(Room room, CancellationToken ct = default);
  }
  ```

## 2. Adapter Implementation Rules
- **Adapter names reflect concrete technologies:**
  - `SqliteOfflineCacheAdapter` implements `ILocalCachePort`.
  - `HttpRoomApiAdapter` implements `IRoomApiClientPort`.
  - `MockRoomApiAdapter` implements `IRoomApiClientPort`.
- When offline testing or mocking is required, swap adapters inside Dependency Injection registration without modifying a single line of business logic in Use Cases.
