---
name: micro-task-planning
description: Systematic decomposition of User Stories into measurable, incremental micro-tasks.
---

# Micro-Task Planning Workflow

Derived from `obra/superpowers/writing-plans`. Breaks broad User Stories into isolated, testable steps to prevent code conflicts and scope creep.

## 1. Decomposition Principles
- Each micro-task should represent no more than 2 hours of engineering effort.
- Every micro-task must include a clear **Definition of Done (DoD)**.
- Strictly adhere to the C# implementation order: **Phase 0: Core Contracts & Stubs -> Phase 1: QA TDD Test (RED) -> Phase 2: Core UseCase (GREEN) -> Infrastructure Adapter -> Phase 3: UI Presentation**.

## 2. Standard Task Breakdown Template for a User Story (Example)

> **Note:** File and class names below are illustrative examples demonstrating project naming conventions (`*UseCaseTests`, `*Request`, `I*UseCase`, `*Adapter`, `*PageModel`). Exact names are defined per User Story in the Mini-Spec.

Example for **US04: Search rooms by filters (Price, Area, Distance)**:

```markdown
### Task 1: [Core] Author Phase 0 DTOs, Port Interfaces & UseCase Stubs (Estimate: 0.5h)
- File: `RoomToRentRadar/Contracts/RoomFilterRequest.cs`
- File: `RoomToRentRadar/Core/Application/Ports/IRoomApiClientPort.cs`
- File: `RoomToRentRadar/Core/Application/UseCases/SearchRooms/ISearchRoomsUseCase.cs`
- File: `RoomToRentRadar/Core/Application/UseCases/SearchRooms/SearchRoomsUseCase.cs` (throws `NotImplementedException`)
- DoD: `dotnet build RoomToRentRadar/RoomToRentRadar.csproj` succeeds with 0 errors.

### Task 2: [QA] Author Unit Tests for Filter UseCase — RED Phase (Estimate: 1h)
- File: `RoomToRentRadarUnitTests/UseCases/SearchRoomsUseCaseTests.cs`
- DoD: Test project compiles with 0 build errors; running `dotnet test` fails at runtime due to `NotImplementedException` (Red phase).

### Task 3: [Core] Implement SearchRoomsUseCase Business Logic — GREEN Phase (Estimate: 1h)
- File: `RoomToRentRadar/Core/Application/UseCases/SearchRooms/SearchRoomsUseCase.cs`
- File: `RoomToRentRadar/MauiProgram.cs` (register `ISearchRoomsUseCase`)
- DoD: Task 2 unit tests transition to 100% passing (Green phase).

### Task 4: [Infra] Build SQLite / Web API Filter Adapter (Estimate: 1.5h)
- File: `RoomToRentRadar/Infrastructure/Services/HttpRoomApiAdapter.cs`
- File: `RoomToRentRadar/MauiProgram.cs` (register `IRoomApiClientPort`)
- DoD: Adapter compiles cleanly and satisfies `IRoomApiClientPort` contract.

### Task 5: [UI] Design Filter Dialog & Wire Up PageModel (Estimate: 2h)
- File: `RoomToRentRadar/Pages/FilterPopupPage.xaml`, `RoomToRentRadar/PageModels/FilterPageModel.cs`
- DoD: Filter popup opens, Syncfusion range slider updates values, applying filters displays the correct result count.
```

## 3. Hand-off Protocol
- A specialist may only begin a subsequent task once the preceding task compiles cleanly (`dotnet build` = 0 errors).
