# RoomToRentRadar — Project Structure & Architecture Development Guide

**Project:** RoomToRentRadar — Student Room Rental Radar  
**Version:** 2.0  
**Document Type:** Project Architecture & Development Guidelines  
**Status:** Proposed — Pre-Implementation  
**Technology Stack:** .NET MAUI, ASP.NET Core Web API, Azure Cosmos DB

---

# 1. Purpose and Scope

## 1.1. Purpose

This document defines the proposed project structure, software architecture, development conventions, dependency rules, and collaboration guidelines for RoomToRentRadar.

Its objectives are to:

- Establish a consistent architecture before implementation begins.
- Define clear responsibilities for each project layer and feature.
- Reduce coupling between components.
- Prevent overlapping responsibilities among team members.
- Make the application easier to maintain, test, and extend.
- Establish common development standards for all team members.
- Define how AI agents and development tools may participate in the project.

This document is a development guideline, not evidence that the proposed structure or functionality has already been implemented.

## 1.2. Architecture Decisions

RoomToRentRadar adopts different architectural approaches for Mobile and Backend.

| Component | Selected Architecture | Main Purpose |
|---|---|---|
| Mobile Application | Feature-Based + MVVM | Organize the application by business features and separate UI from presentation logic |
| Backend API | Hexagonal Architecture (Ports & Adapters) | Isolate business logic from external technologies and infrastructure |
| Database | Azure Cosmos DB | Persistent data storage, accessed through Backend Adapters |
| API Communication | REST API | Establish communication contracts between Mobile and Backend |
| Dependency Management | Dependency Injection | Manage dependencies and implementations |

**Important:** Hexagonal Architecture applies to the Backend only. The Mobile application must retain Feature-Based + MVVM as its primary architecture.

## 1.3. Project Scope

RoomToRentRadar is a mobile application intended to help ICTU students discover and evaluate rental rooms near the university.

The proposed system includes:

- Room discovery and listing management.
- Room details and comparison.
- Search and filtering.
- Nearby amenities and location information.
- Area safety information.
- Roommate matching.
- Booking and deposit payment.
- User profiles and authentication.
- Administrative and listing verification workflows.

The exact implementation scope and feature priority must follow the approved Product Backlog.

---

# 2. Overall System Architecture

## 2.1. High-Level Architecture

The system consists of three major components:

1. Mobile Application — User interface and mobile-side application logic.
2. Backend API — Business rules, use cases, data access coordination, and external integrations.
3. Database and External Services — Persistent storage and third-party integrations.

```text
                    USERS
                      |
                      v
          +-------------------------+
          |    .NET MAUI Mobile     |
          |                         |
          | Feature-Based + MVVM    |
          +-------------------------+
                      |
                 REST API
                      |
                      v
          +-------------------------+
          |     ASP.NET Core API    |
          |                         |
          |   Hexagonal Architecture|
          |                         |
          |  Inbound Adapters       |
          |  Application / Ports    |
          |  Domain                 |
          |  Outbound Adapters      |
          +-------------------------+
                      |
             +--------+---------+
             |                  |
             v                  v
      +-------------+    +----------------+
      | Azure       |    | External       |
      | Cosmos DB   |    | Services       |
      +-------------+    +----------------+
```

## 2.2. Architectural Boundaries

Each component has a distinct responsibility.

| Component | Responsibility | Must Not |
|---|---|---|
| Mobile | UI, navigation, user interaction, presentation state | Implement authoritative Backend business rules |
| Backend Domain | Core business rules and domain concepts | Depend on database, HTTP, or UI frameworks |
| Backend Application | Coordinate use cases and define ports | Contain infrastructure-specific implementations |
| Backend Adapters | Translate external requests and infrastructure operations | Become the owner of core business rules |
| Database | Persist application data | Be accessed directly by Mobile |

## 2.3. General Principles

All development must follow these principles:

- **Separation of Concerns:** Each component has one clearly defined responsibility.
- **Single Responsibility Principle:** A class or module should have one primary reason to change.
- **High Cohesion:** Related functionality should stay together.
- **Low Coupling:** Components should depend on stable contracts rather than concrete implementations.
- **Dependency Inversion:** High-level business logic must not depend on low-level infrastructure details.
- **DRY:** Avoid unnecessary duplication.
- **KISS:** Prefer simple and understandable solutions.
- **YAGNI:** Do not implement speculative features.
- **Explicit over Implicit:** Important dependencies and behaviors should be clear.
- **Consistency:** Follow established project conventions instead of introducing personal conventions.

---

# 3. Mobile Application Architecture

## 3.1. Selected Architecture: Feature-Based + MVVM

The Mobile application uses Feature-Based Architecture combined with Model-View-ViewModel (MVVM).

The application is organized primarily by business features rather than by placing all Views, ViewModels, and Services into global folders.

MVVM separates presentation responsibilities:

| Component | Responsibility |
|---|---|
| View | Display UI and collect user interactions |
| ViewModel | Manage presentation state and coordinate UI actions |
| Model | Represent data used by the feature |
| Service | Provide feature-specific operations or communicate through shared abstractions |

The goal is to make each feature relatively independent and easier for different team members to develop.

## 3.2. Proposed Mobile Project Structure

```text
RoomToRentRadar/
│
├── src/
│   │
│   ├── RoomToRentRadar.Mobile/
│   │   │
│   │   ├── Features/
│   │   │   ├── Authentication/
│   │   │   │   ├── Views/
│   │   │   │   ├── ViewModels/
│   │   │   │   ├── Models/
│   │   │   │   └── Services/
│   │   │   │
│   │   │   ├── Home/
│   │   │   │   ├── Views/
│   │   │   │   ├── ViewModels/
│   │   │   │   └── Models/
│   │   │   │
│   │   │   ├── RoomListings/
│   │   │   │   ├── Views/
│   │   │   │   ├── ViewModels/
│   │   │   │   ├── Models/
│   │   │   │   └── Services/
│   │   │   │
│   │   │   ├── RoomDetails/
│   │   │   │   ├── Views/
│   │   │   │   ├── ViewModels/
│   │   │   │   └── Models/
│   │   │   │
│   │   │   ├── Booking/
│   │   │   │   ├── Views/
│   │   │   │   ├── ViewModels/
│   │   │   │   └── Models/
│   │   │   │
│   │   │   ├── Payment/
│   │   │   │   ├── Views/
│   │   │   │   ├── ViewModels/
│   │   │   │   └── Models/
│   │   │   │
│   │   │   ├── RoommateMatching/
│   │   │   │   ├── Views/
│   │   │   │   ├── ViewModels/
│   │   │   │   └── Models/
│   │   │   │
│   │   │   └── Profile/
│   │   │       ├── Views/
│   │   │       ├── ViewModels/
│   │   │       └── Models/
│   │   │
│   │   ├── Core/
│   │   │   ├── Interfaces/
│   │   │   ├── Models/
│   │   │   ├── Exceptions/
│   │   │   ├── Constants/
│   │   │   └── Extensions/
│   │   │
│   │   ├── Infrastructure/
│   │   │   ├── Api/
│   │   │   ├── Authentication/
│   │   │   ├── Storage/
│   │   │   ├── Location/
│   │   │   ├── Notifications/
│   │   │   └── DependencyInjection/
│   │   │
│   │   ├── Shared/
│   │   │   ├── Controls/
│   │   │   ├── Converters/
│   │   │   ├── Behaviors/
│   │   │   ├── Styles/
│   │   │   └── Templates/
│   │   │
│   │   ├── Resources/
│   │   ├── Platforms/
│   │   ├── App.xaml
│   │   ├── AppShell.xaml
│   │   └── MauiProgram.cs
│   │
│   └── RoomToRentRadar.Mobile.Tests/
│
├── tests/
│   ├── RoomToRentRadar.Mobile.UnitTests/
│   └── RoomToRentRadar.Mobile.UITests/
│
├── docs/
│
└── README.md
```

This is a proposed structure. Folders should be created when the corresponding implementation requires them; empty folders do not need to be created merely to match the diagram.

## 3.3. Feature Organization Rules

Each feature owns its presentation-related components.

For example:

```text
Features/
└── RoomListings/
    ├── Views/
    │   ├── RoomListingsPage.xaml
    │   └── RoomFilterView.xaml
    │
    ├── ViewModels/
    │   ├── RoomListingsViewModel.cs
    │   └── RoomFilterViewModel.cs
    │
    ├── Models/
    │   ├── RoomListItemModel.cs
    │   └── RoomFilterModel.cs
    │
    └── Services/
        └── RoomListingsService.cs
```

Rules:

1. A feature should contain the components that belong to that feature.
2. A View should not contain complex business or data-access logic.
3. A ViewModel should manage presentation state and coordinate operations.
4. A ViewModel must not directly instantiate infrastructure implementations.
5. Feature-specific models should remain inside their feature unless reuse is justified.
6. Shared components should only be moved into `Core` or `Shared` when there is a real reuse requirement.
7. A feature must not directly manipulate another feature's internal ViewModel or View.

## 3.4. MVVM Rules

### View

Responsible for:

- Displaying data.
- Binding UI elements to ViewModel properties.
- Forwarding user interactions through commands.
- Defining visual layout and UI behavior.

Not responsible for:

- Calling Backend APIs directly.
- Performing database operations.
- Implementing complex business rules.
- Managing application-wide business workflows.

### ViewModel

Responsible for:

- Exposing bindable properties.
- Managing loading, success, empty, and error states.
- Handling UI commands.
- Coordinating service calls.
- Preparing data for presentation.

Not responsible for:

- Directly creating `HttpClient` instances.
- Accessing Cosmos DB.
- Implementing Backend authorization rules.
- Containing UI layout definitions.

### Model

Responsible for representing data needed by the feature.

Do not automatically reuse every API DTO as a UI model. Transform data when the presentation requirements differ from the API contract.

## 3.5. Mobile Dependency Rules

The intended dependency direction is:

```text
View
 |
 v
ViewModel
 |
 v
Interface / Service Contract
 |
 v
Infrastructure Implementation
```

Rules:

- View depends on its ViewModel.
- ViewModel depends on abstractions or application-facing services.
- Infrastructure implements the required abstractions.
- Dependency Injection connects abstractions to implementations.
- Features must not depend on the private implementation details of other features.
- Shared code must not depend on individual features.

Mobile may use interfaces and dependency inversion where useful. However, it does not need a separate Hexagonal Architecture structure with formal inbound and outbound adapter layers.

## 3.6. Mobile Navigation

Navigation must follow a consistent approach.

- Centralize application-level route registration.
- Use meaningful and consistent route names.
- Avoid hardcoding navigation logic throughout unrelated components.
- Pass identifiers or necessary navigation parameters instead of passing entire complex ViewModels.
- Keep navigation behavior separate from feature business logic where practical.

The final navigation mechanism should be selected consistently during implementation.

## 3.7. Mobile API Communication

All Backend communication must go through designated API services or clients.

Example:

```text
Features/
└── RoomListings/
    └── ViewModels/
        └── RoomListingsViewModel.cs

Infrastructure/
└── Api/
    ├── ApiClient.cs
    ├── RoomApiService.cs
    └── ApiExceptionHandler.cs
```

The ViewModel must not construct HTTP requests directly.

API services are responsible for:

- Sending HTTP requests.
- Serializing and deserializing API data.
- Handling HTTP status codes.
- Mapping transport errors to application-facing errors.
- Applying shared API configuration.

Do not duplicate authentication headers, base URLs, or common HTTP configuration in every feature.

## 3.8. Mobile Dependency Injection

Register dependencies centrally through the MAUI startup configuration or dedicated registration extensions.

Example:

```csharp
builder.Services.AddSingleton<IApiClient, ApiClient>();
builder.Services.AddTransient<IRoomApiService, RoomApiService>();

builder.Services.AddTransient<RoomListingsViewModel>();
builder.Services.AddTransient<RoomListingsPage>();
```

The lifetime shown is illustrative. Select `Singleton`, `Scoped`, or `Transient` according to the actual service lifecycle and MAUI application behavior.

Avoid service location patterns and unnecessary static service containers.

---

# 4. Backend Architecture

## 4.1. Selected Architecture: Hexagonal Architecture

The Backend uses Hexagonal Architecture, also known as Ports and Adapters.

The central objective is to isolate the business domain and application use cases from external technologies.

The system must allow the core application logic to operate without depending directly on:

- ASP.NET Core.
- Azure Cosmos DB.
- External payment providers.
- Authentication providers.
- Notification services.
- Other infrastructure technologies.

The Backend is organized around three main concepts:

1. Domain — Core business concepts and rules.
2. Application — Use cases and ports.
3. Adapters — Communication with external systems.

A Bootstrap/Composition Root component is responsible for configuring and connecting the implementations.

## 4.2. Hexagonal Architecture Concepts

### Domain

Contains core business concepts and domain rules.

Examples:

- Room.
- User.
- Booking.
- Payment.
- RoommateProfile.
- ListingVerification.

Domain must not depend on:

- ASP.NET Core.
- Cosmos DB SDK.
- HTTP request or response types.
- Database-specific document models.
- External infrastructure implementations.

### Application

Coordinates application use cases and defines the ports required by those use cases.

Examples:

- CreateRoomListing.
- SearchRoomListings.
- GetRoomDetails.
- CreateBooking.
- ConfirmPayment.
- VerifyRoomListing.

Application contains:

- Use cases.
- Inbound port contracts where applicable.
- Outbound port interfaces.
- Application DTOs.
- Application-level validation and orchestration.

Application must not directly implement database or external API communication.

### Inbound Adapters

Inbound Adapters allow external actors to interact with application use cases.

Examples:

- REST API Controllers.
- HTTP endpoints.
- Request and response mapping.
- API-specific validation.
- Authentication and authorization integration at the API boundary.

Controllers should remain thin. They should translate HTTP requests into application operations rather than own the business rules.

### Outbound Adapters

Outbound Adapters implement interfaces defined by the Application layer.

Examples:

- Cosmos DB repositories.
- Payment gateway integrations.
- Email providers.
- Notification providers.
- External location or map services.

Outbound Adapters translate application-level requests into technology-specific operations.

### Bootstrap / Composition Root

Responsible for:

- Registering dependencies.
- Connecting ports to adapters.
- Configuring application services.
- Configuring infrastructure.
- Setting up the ASP.NET Core application.

It is the place where concrete implementations are wired together.

## 4.3. Proposed Backend Project Structure

```text
RoomToRentRadar/
│
├── src/
│   │
│   ├── RoomToRentRadar.Domain/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Enums/
│   │   ├── DomainServices/
│   │   ├── Events/
│   │   └── Exceptions/
│   │
│   ├── RoomToRentRadar.Application/
│   │   ├── Ports/
│   │   │   ├── Inbound/
│   │   │   └── Outbound/
│   │   │
│   │   ├── UseCases/
│   │   │   ├── Authentication/
│   │   │   ├── RoomListings/
│   │   │   ├── RoomDetails/
│   │   │   ├── Booking/
│   │   │   ├── Payment/
│   │   │   ├── RoommateMatching/
│   │   │   └── Profile/
│   │   │
│   │   ├── DTOs/
│   │   ├── Validators/
│   │   ├── Mappings/
│   │   └── Exceptions/
│   │
│   ├── RoomToRentRadar.Adapters/
│   │   ├── Inbound/
│   │   │   └── WebApi/
│   │   │       ├── Controllers/
│   │   │       ├── Middleware/
│   │   │       ├── Filters/
│   │   │       ├── Contracts/
│   │   │       └── Mappings/
│   │   │
│   │   └── Outbound/
│   │       ├── Persistence/
│   │       │   └── CosmosDb/
│   │       │       ├── Repositories/
│   │       │       ├── Documents/
│   │       │       ├── Configurations/
│   │       │       └── Mappings/
│   │       │
│   │       ├── Authentication/
│   │       ├── Payment/
│   │       ├── Notifications/
│   │       ├── Email/
│   │       └── ExternalServices/
│   │
│   └── RoomToRentRadar.Bootstrap/
│       ├── DependencyInjection/
│       ├── Configuration/
│       ├── Extensions/
│       ├── Program.cs
│       └── appsettings.json
│
├── tests/
│   ├── RoomToRentRadar.Domain.Tests/
│   ├── RoomToRentRadar.Application.Tests/
│   ├── RoomToRentRadar.Adapters.Tests/
│   └── RoomToRentRadar.IntegrationTests/
│
├── docs/
│
└── README.md
```

This is a logical project structure. The final number of .NET projects and their physical boundaries may be adjusted based on implementation needs.

Do not create separate projects or folders for every minor class without a clear reason.

## 4.4. Backend Dependency Rules

The most important rule of Hexagonal Architecture is that dependencies must point toward the application core.

```text
              External Actors
                     |
                     v
          +----------------------+
          |   Inbound Adapters   |
          |   REST API           |
          +----------------------+
                     |
                     v
          +----------------------+
          |     Application      |
          |                      |
          |     Use Cases        |
          |     Inbound Ports    |
          |     Outbound Ports   |
          +----------------------+
                     |
                     v
          +----------------------+
          |        Domain        |
          |                      |
          | Entities             |
          | Value Objects        |
          | Business Rules       |
          +----------------------+

          Application Outbound Ports
                     ^
                     |
          +----------------------+
          |   Outbound Adapters  |
          |                      |
          | Cosmos DB            |
          | Payment              |
          | Notifications        |
          +----------------------+
```

Dependency rules:

1. Domain must not depend on Application, Adapters, or Bootstrap.
2. Application may depend on Domain.
3. Application defines outbound interfaces required by its use cases.
4. Outbound Adapters implement Application-defined interfaces.
5. Inbound Adapters invoke Application use cases through application-facing contracts.
6. Bootstrap may reference the concrete projects to configure Dependency Injection.
7. Domain and Application must not reference Cosmos DB SDK or ASP.NET Core types.
8. Infrastructure-specific details must not leak into the core.

## 4.5. Ports

Ports define the contracts through which the application interacts with the outside world.

### Inbound Ports

Inbound Ports represent operations that the application exposes.

Examples:

```csharp
public interface ISearchRoomListingsUseCase
{
    Task<SearchRoomListingsResult> ExecuteAsync(
        SearchRoomListingsQuery query,
        CancellationToken cancellationToken);
}
```

```csharp
public interface ICreateBookingUseCase
{
    Task<CreateBookingResult> ExecuteAsync(
        CreateBookingCommand command,
        CancellationToken cancellationToken);
}
```

### Outbound Ports

Outbound Ports represent external capabilities required by application use cases.

Example:

```csharp
public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(
        string roomId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Room>> SearchAsync(
        RoomSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task AddAsync(
        Room room,
        CancellationToken cancellationToken);
}
```

The interface describes what the application needs, not how Cosmos DB performs the operation.

## 4.6. Adapters

An Adapter implements or exposes a Port through a particular technology.

Example:

```text
Application/
└── Ports/
    └── Outbound/
        └── IRoomRepository.cs

Adapters/
└── Outbound/
    └── Persistence/
        └── CosmosDb/
            └── CosmosRoomRepository.cs
```

The repository implementation is responsible for Cosmos DB-specific behavior.

The Application layer must not know whether the data is stored in Cosmos DB, SQL Server, or another persistence technology.

## 4.7. Domain Rules

Domain contains the authoritative business rules of the system.

Examples:

- A room listing must satisfy required information before publication.
- A booking must follow valid booking-state transitions.
- A payment must not be treated as successful merely because a request was received.
- A verified listing must satisfy the required verification conditions.
- Roommate matching must respect the defined matching criteria.

These are examples of possible domain rules. Their final definitions must be based on the approved Business Rules and Acceptance Criteria.

Do not place business rules exclusively inside Controllers, repositories, or database queries.

## 4.8. Backend API Rules

Controllers are Inbound Adapters.

They should:

- Receive HTTP requests.
- Validate transport-level input.
- Invoke the appropriate application use case.
- Translate application results into HTTP responses.
- Return appropriate status codes.

They should not:

- Directly access Cosmos DB.
- Implement complex business workflows.
- Contain domain state-transition rules.
- Instantiate repositories manually.
- Duplicate Application validation.

## 4.9. Persistence and Cosmos DB

Cosmos DB is an infrastructure technology, not part of the Domain.

Rules:

1. Cosmos DB SDK usage must stay inside the Persistence Adapter.
2. Application use cases must depend on repository interfaces.
3. Database document models should be separated from Domain entities when their structures differ.
4. Mapping between database documents and Domain entities belongs in the persistence adapter.
5. Partition-key strategy must be considered during data-model design.
6. Query patterns and indexing must be evaluated against actual application requirements.
7. Do not expose Cosmos DB-specific response types to Application or Domain.
8. Do not make Controllers responsible for database persistence.

The team must document important data-model and partition-key decisions before implementing the affected features.

## 4.10. Backend Dependency Injection

Dependency Injection is configured in Bootstrap.

Example:

```csharp
services.AddScoped<ISearchRoomListingsUseCase, SearchRoomListingsUseCase>();

services.AddScoped<IRoomRepository, CosmosRoomRepository>();
```

The exact service lifetime must be selected according to the actual implementation and resource lifecycle.

Avoid:

- Manually constructing infrastructure implementations in use cases.
- Static service locators.
- Circular dependencies.
- Registering the same service inconsistently in multiple places.

---

# 5. API Contract and Mobile–Backend Integration

## 5.1. Contract-First Collaboration

Mobile and Backend teams must agree on API contracts before implementing dependent integration work.

An API contract should define:

- HTTP method.
- Endpoint.
- Request schema.
- Response schema.
- Authentication requirements.
- Authorization requirements.
- Validation rules.
- Error response format.
- Pagination and filtering behavior, when applicable.

Example:

```http
GET /api/rooms?keyword=student&minPrice=1000000
```

Example response:

```json
{
  "items": [
    {
      "id": "room-001",
      "title": "Student Room Near Campus",
      "price": 1800000,
      "address": "Example Address",
      "isVerified": true
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1
}
```

These examples are illustrative only. They are not approved production API contracts.

## 5.2. API Change Management

When an API contract changes:

1. Identify affected Mobile features.
2. Identify affected Backend use cases and adapters.
3. Update the API documentation.
4. Notify dependent team members.
5. Update tests and mocks.
6. Ensure both sides agree on the revised contract.

Do not silently change a request or response structure when another team member is implementing against it.

## 5.3. Error Handling

The Backend should return consistent error responses.

The Mobile application should:

- Handle network failures.
- Handle authentication failures.
- Handle validation errors.
- Handle unexpected server errors.
- Show understandable messages to users.
- Avoid displaying raw exception details.

Sensitive infrastructure details and stack traces must not be exposed in public API responses.

---

# 6. Shared Development Standards

## 6.1. Naming Conventions

Use consistent naming conventions across the codebase.

| Element | Convention | Example |
|---|---|---|
| Class | PascalCase | `RoomService` |
| Interface | PascalCase with I prefix | `IRoomRepository` |
| Method | PascalCase | `GetRoomByIdAsync` |
| Property | PascalCase | `RoomName` |
| Private field | camelCase with underscore | `_roomRepository` |
| Parameter | camelCase | `roomId` |
| Constant | PascalCase | `DefaultPageSize` |
| Namespace | PascalCase | `RoomToRentRadar.Application` |

Use meaningful names that describe intent.

Avoid ambiguous names such as:

- `Data`
- `Manager`
- `Helper`
- `Common`
- `Process`
- `DoSomething`

These names are not prohibited in every situation, but they must not replace a meaningful domain or responsibility-specific name.

## 6.2. Code Quality

All code must:

- Be readable and maintainable.
- Follow the established project conventions.
- Avoid unnecessary duplication.
- Use asynchronous operations appropriately.
- Handle expected failures.
- Avoid unnecessary comments that simply repeat the code.
- Include comments for non-obvious business decisions and important technical constraints.
- Avoid unexplained magic numbers and strings.
- Avoid swallowing exceptions silently.

## 6.3. Async and Cancellation

For asynchronous Backend operations:

- Use `async` and `await` appropriately.
- Use `CancellationToken` for operations where cancellation is relevant.
- Avoid blocking asynchronous execution with `.Result` or `.Wait()`.
- Do not introduce unnecessary asynchronous wrappers around synchronous operations.

Apply the same principles to Mobile asynchronous API operations.

## 6.4. Configuration and Secrets

Never commit:

- API keys.
- Passwords.
- Access tokens.
- Private certificates.
- Production connection strings.
- Other confidential credentials.

Use appropriate local configuration and environment-specific secret management.

Commit only safe example configuration when necessary.

---

# 7. Testing Strategy

Testing should be performed at the appropriate architectural boundary.

## 7.1. Mobile Testing

| Test Type | Scope |
|---|---|
| Unit Tests | ViewModels, presentation logic, feature services |
| UI Tests | Important user interactions and navigation |
| Integration Tests | API client behavior and integration with dependent services |

Mobile ViewModels should be testable without requiring real HTTP calls.

Use mocks or fakes for external dependencies.

## 7.2. Backend Testing

| Test Type | Scope |
|---|---|
| Domain Unit Tests | Entities, value objects, domain rules |
| Application Unit Tests | Use cases with mocked outbound ports |
| Adapter Tests | HTTP mapping, repository implementation, external integration |
| Integration Tests | Actual interaction between application components and configured infrastructure |

## 7.3. Testing Rules

- Test important business rules.
- Test failure cases, not only successful cases.
- Test authorization boundaries.
- Test invalid input and state transitions.
- Avoid writing tests that only duplicate implementation details.
- Keep tests independent where possible.
- Do not use production credentials or production data in automated tests.

## 7.4. Definition of Done for Tests

A task involving code is not complete merely because it compiles.

The developer must:

- Run relevant tests.
- Verify affected functionality.
- Fix regressions introduced by the change.
- Report tests that could not be executed and explain why.

---

# 8. Team Collaboration and Ownership

## 8.1. Ownership Principles

Every task must have:

- One clearly defined owner.
- A defined scope.
- Expected inputs and outputs.
- Dependencies on other tasks.
- Acceptance Criteria.
- A clear completion condition.

Other members may review or support the task, but must not independently modify the same responsibility without coordination.

## 8.2. Mobile and Backend Responsibilities

### Mobile Team

Responsible for:

- MAUI UI implementation.
- Feature-based organization.
- MVVM implementation.
- Navigation.
- Presentation state.
- API client integration.
- Mobile-side validation for user experience.
- Device-specific capabilities.

Not responsible for:

- Owning authoritative business rules.
- Directly accessing the Backend database.
- Bypassing Backend authorization.
- Reimplementing payment verification logic as a trusted operation.

### Backend Team

Responsible for:

- Domain modeling.
- Application use cases.
- Ports and Adapters.
- API endpoints.
- Authentication and authorization enforcement.
- Business validation.
- Data persistence.
- External service integration.
- API documentation.

Not responsible for:

- Implementing Mobile UI.
- Defining presentation-only behavior without Mobile coordination.
- Silently changing API contracts.

## 8.3. Shared Responsibilities

The following require coordination:

- API contracts.
- Domain terminology.
- Request and response DTOs.
- Authentication flows.
- Error response conventions.
- Pagination and filtering.
- Feature acceptance criteria.
- Integration testing.

## 8.4. Avoiding File Ownership Conflicts

When multiple developers work on related features:

1. Assign ownership by feature or architectural responsibility.
2. Avoid assigning the same file to multiple developers simultaneously.
3. Identify shared-file changes before starting parallel implementation.
4. Communicate changes to shared interfaces before dependent work begins.
5. Use Pull Requests to review cross-feature changes.

---

# 9. Git and Pull Request Guidelines

## 9.1. Branching Strategy

Use a consistent branching strategy.

Suggested branches:

```text
main
develop

feature/<feature-name>
fix/<issue-name>
refactor/<scope>
docs/<document-name>
```

Examples:

```text
feature/room-listings
feature/booking
feature/payment-integration
fix/room-search-validation
docs/api-contract
```

The team should agree on the exact branch protection settings before implementation.

## 9.2. Commit Convention

Use meaningful commit messages.

Suggested format:

```text
<type>(<scope>): <description>
```

Examples:

```text
feat(room-listings): add room search endpoint
feat(authentication): implement login screen
fix(booking): prevent invalid booking transition
refactor(persistence): isolate cosmos repository mapping
docs(api): update room listing contract
test(booking): add booking validation tests
```

Suggested types:

- `feat`
- `fix`
- `refactor`
- `docs`
- `test`
- `build`
- `ci`
- `chore`

## 9.3. Pull Request Rules

Every Pull Request should:

- Have a clear title.
- Explain the purpose of the change.
- Reference the related task or issue.
- Describe affected features or layers.
- Include relevant testing information.
- Mention API or data-model changes.
- Include screenshots for significant UI changes when appropriate.

Before merging:

- Ensure the branch is up to date with the target branch.
- Resolve merge conflicts.
- Ensure CI checks pass.
- Obtain the required review approval.
- Address important review comments.
- Confirm that the change does not violate architectural dependency rules.

Avoid combining unrelated features or refactoring into one Pull Request.

## 9.4. Code Review Priorities

Reviewers should prioritize:

1. Incorrect business logic.
2. Security and authorization issues.
3. Architectural dependency violations.
4. Data integrity and persistence issues.
5. API contract inconsistencies.
6. Error handling and failure scenarios.
7. Performance problems.
8. Maintainability and coding conventions.

Avoid blocking a Pull Request over purely stylistic preferences that do not affect the agreed standards.

---

# 10. Documentation Standards

The project should maintain documentation that helps team members understand and implement the system consistently.

Suggested documentation structure:

```text
docs/
│
├── architecture/
│   ├── system-overview.md
│   ├── mobile-architecture.md
│   ├── backend-hexagonal-architecture.md
│   └── architecture-decisions/
│
├── api/
│   ├── api-overview.md
│   ├── authentication-api.md
│   ├── room-listings-api.md
│   └── booking-api.md
│
├── database/
│   ├── data-model.md
│   └── cosmos-db-design.md
│
├── development/
│   ├── setup-guide.md
│   ├── coding-standards.md
│   ├── git-workflow.md
│   └── testing-guide.md
│
└── team/
    ├── responsibilities.md
    └── contribution-guide.md
```

Not every document must exist before development begins. Create and update documentation as the corresponding decisions and implementation details become available.

## 10.1. Architecture Decision Records (ADR)

Important architecture decisions should be recorded.

An ADR should contain:

- Decision title.
- Context and problem.
- Considered alternatives.
- Chosen solution.
- Reasons for the decision.
- Consequences and trade-offs.
- Status and date.

Examples of decisions worth recording:

- Choosing Hexagonal Architecture for Backend.
- Choosing Feature-Based + MVVM for Mobile.
- Selecting Cosmos DB partition keys.
- Defining authentication strategy.
- Choosing the navigation approach in MAUI.
- Defining API versioning conventions.

---

# 11. AI Agent and Development Tool Governance

AI tools may assist the team with analysis, implementation, documentation, testing, and code review.

However, AI-generated output must follow the same project architecture and quality requirements as human-written code.

## 11.1. Permitted Activities

AI agents may:

- Explain the existing architecture.
- Analyze requirements and Acceptance Criteria.
- Suggest implementation approaches.
- Generate code within an assigned scope.
- Create unit tests.
- Review code for defects.
- Generate or update technical documentation.
- Identify dependency violations.
- Suggest refactoring opportunities.

## 11.2. Restricted Activities

AI agents must not independently:

- Change the approved architecture without team approval.
- Modify unrelated features.
- Change public API contracts without coordination.
- Introduce new dependencies without justification and approval.
- Remove tests to make CI pass.
- Modify production secrets or credentials.
- Bypass authentication or authorization.
- Perform destructive database operations without explicit approval.
- Merge or release changes without the required authorization.

## 11.3. AI Output Verification

All AI-generated code must be reviewed by a responsible developer.

The developer remains responsible for:

- Correctness.
- Security.
- Architecture compliance.
- Test coverage.
- Integration behavior.
- Final approval.

AI-generated code must not be treated as correct merely because it compiles.

---

# 12. Development Workflow

The recommended workflow is:

## Step 1 — Requirement Analysis

- Read the Product Backlog.
- Review the relevant Epic and User Story.
- Understand the Acceptance Criteria.
- Identify business rules.
- Identify dependencies.

## Step 2 — Architecture and Contract Preparation

- Identify the affected Mobile feature.
- Identify the corresponding Backend use case.
- Define required Ports and Adapters for Backend work.
- Agree on API contracts.
- Identify shared models and integration requirements.

## Step 3 — Task Assignment

- Assign a clear owner.
- Define the scope.
- Identify affected files and modules.
- Identify dependent tasks.
- Agree on completion criteria.

## Step 4 — Implementation

- Follow the selected architecture.
- Respect dependency rules.
- Avoid unrelated changes.
- Add appropriate error handling.
- Write tests for important logic.

## Step 5 — Local Verification

- Build the affected project.
- Run relevant tests.
- Verify the expected behavior.
- Check for regressions.
- Review the diff.

## Step 6 — Pull Request

- Create a focused Pull Request.
- Explain the change.
- Provide test evidence.
- Request review.
- Address important comments.

## Step 7 — Integration

- Verify Mobile–Backend compatibility.
- Check API contract consistency.
- Run integration tests where applicable.
- Update documentation.

---

# 13. Definition of Done

A User Story or development task is considered complete when:

- Its Acceptance Criteria are satisfied.
- Its implementation follows the approved architecture.
- Its dependencies are correctly managed.
- Relevant error scenarios are handled.
- Required tests are written and executed.
- No known critical regression is introduced.
- Code review is completed.
- Required documentation is updated.
- The change is merged through the agreed workflow.

A task must not be marked complete solely because its UI has been created or its API endpoint returns a successful response.

---

# 14. Practices to Avoid

The following practices are discouraged or prohibited when they violate the project's architecture and security requirements.

## Mobile

- Putting API calls directly inside Views.
- Putting complex business workflows inside ViewModels.
- Accessing Backend databases directly.
- Duplicating common API configuration in multiple features.
- Creating unnecessary global shared classes.
- Allowing unrelated features to access each other's internal implementation.
- Building abstractions for hypothetical future requirements.

## Backend

- Putting business logic inside Controllers.
- Allowing Domain to reference Cosmos DB SDK.
- Allowing Application use cases to instantiate database repositories directly.
- Returning database-specific document models as public API contracts.
- Placing all business operations in one large service class.
- Creating interfaces without a meaningful abstraction boundary.
- Allowing external infrastructure details to leak into Domain.
- Bypassing Application use cases for important business operations.

## Team Workflow

- Committing directly to protected branches.
- Implementing against undocumented API assumptions.
- Modifying another developer's scope without coordination.
- Merging code without required review.
- Committing credentials or sensitive configuration.
- Marking incomplete or unverified work as completed.

---

# 15. Architecture Change Management

The architecture defined in this document is the team's agreed starting point.

It may evolve when implementation reveals genuine technical or business requirements.

However, changes must be justified.

Before changing an architectural boundary:

1. Describe the existing problem.
2. Explain why the current structure is insufficient.
3. Consider simpler alternatives.
4. Evaluate the impact on dependent features.
5. Discuss the proposal with the team.
6. Record significant decisions in an ADR.
7. Update this document when the approved architecture changes.

Do not introduce additional architectural layers simply because a pattern is popular.

The objective is to maintain a structure that is understandable, testable, and appropriate for the actual project.

---

# 16. Final Architecture Agreement

The following decisions are the baseline for RoomToRentRadar development.

| Area | Agreed Direction |
|---|---|
| Mobile Architecture | Feature-Based + MVVM |
| Backend Architecture | Hexagonal Architecture (Ports & Adapters) |
| Mobile UI | .NET MAUI |
| Backend Framework | ASP.NET Core |
| Database | Azure Cosmos DB |
| Mobile–Backend Communication | REST API |
| Dependency Management | Dependency Injection |
| Backend Business Rules | Domain and Application |
| Backend Data Access | Outbound Adapters |
| Backend HTTP Entry Point | Inbound Adapters |
| Testing | Unit, Adapter, Integration, and UI Tests |
| Collaboration | Feature/responsibility ownership and Pull Requests |
| Documentation | Architecture, API, Database, and Development Guidelines |

## Final Principle

**Mobile organizes code around business features and presentation responsibilities. Backend organizes code around business capabilities, application use cases, and external system boundaries.**

Both sides must follow clear dependency rules, maintain well-defined contracts, and coordinate changes through the team's agreed development workflow.

This architecture is intended to provide a practical foundation for the project without introducing unnecessary complexity before implementation.