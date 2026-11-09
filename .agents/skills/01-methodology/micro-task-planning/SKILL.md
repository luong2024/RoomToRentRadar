---
name: micro-task-planning
description: Systematic decomposition of User Stories into measurable, incremental micro-tasks.
---

# Micro-Task Planning Workflow

Derived from `obra/superpowers/writing-plans`. Breaks broad User Stories into isolated, testable steps to prevent code conflicts and scope creep.

## 1. Decomposition Principles
- Each micro-task should represent no more than 2 hours of engineering effort.
- Every micro-task must include a clear **Definition of Done (DoD)**.
- Strictly adhere to the implementation order: **TDD Test -> Core UseCase -> Infrastructure Adapter -> UI Presentation**.

## 2. Standard Task Breakdown Template for a User Story (Example)

> **Note:** File and class names below are illustrative examples demonstrating project naming conventions (`*UseCaseTests`, `*Request`, `I*UseCase`, `*Adapter`, `*PageModel`). Exact names are defined per User Story in the Mini-Spec.

Example for **US04: Search rooms by filters (Price, Area, Distance)**:

```markdown
### Task 1: [QA] Author Unit Tests for Filter UseCase (Estimate: 1h)
- File: `RoomToRentRadarUnitTests/UseCases/SearchRoomsUseCaseTests.cs`
- DoD: Running `dotnet test` fails due to the missing UseCase behavior (Red phase).

### Task 2: [Core] Implement Request DTO and UseCase (Estimate: 1.5h)
- File: `Core/Application/UseCases/SearchRooms/RoomFilterRequest.cs`
- File: `Core/Application/UseCases/SearchRooms/SearchRoomsUseCase.cs`
- DoD: Task 1 tests transition to passing (Green phase).

### Task 3: [Infra] Build SQLite / Web API Filter Adapter (Estimate: 1.5h)
- File: `Infrastructure/Services/HttpRoomApiAdapter.cs`
- DoD: API call succeeds with valid query parameters.

### Task 4: [UI] Design Filter Dialog & Wire Up PageModel (Estimate: 2h)
- File: `Pages/FilterPopupPage.xaml`, `PageModels/FilterPageModel.cs`
- DoD: Filter popup opens, Syncfusion range slider updates values, applying filters displays the correct result count.
```

## 3. Hand-off Protocol
- A specialist may only begin a subsequent task once the preceding task compiles cleanly (`dotnet build` = 0 errors).
