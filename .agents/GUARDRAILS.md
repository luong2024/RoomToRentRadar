# AGENT OPERATIONAL GUARDRAILS (PROJECT SAFETY POLICY)
> **Project:** RoomToRentRadar — Group 2 (ICTU)  
> **Platform:** .NET MAUI 10 (Client) + ASP.NET Core (REST API) + SQLite (single datastore — `docs/adr/adr-002`)

---

## 1. INVIOLABLE CORE PRINCIPLES

Every Agent and Subagent operating within this project MUST strictly comply with the following policies:

### 1.1. Compilation First
- Every code modification cycle MUST conclude with verification via the build commands for both Client and Backend API:
  ```powershell
  dotnet build RoomToRentRadar/RoomToRentRadar.csproj
  dotnet build RoomToRentRadar.Api/RoomToRentRadar.Api.csproj
  ```
- If any compilation error persists, the Agent **IS FORBIDDEN** from reporting the task as complete to the user.

### 1.2. Strict Scope Sandboxing
- Each prompt iteration must strictly operate within the boundaries of the assigned User Story.
- **The Two-File Rule:** If fixing an issue causes cascading errors in **more than 2 files outside the current task's scope**, the Agent **MUST STOP IMMEDIATELY** and request user guidance. Cascading refactoring is strictly prohibited.
- **QA Subagent Sandbox:** The QA subagent is only permitted to create and modify test files within `RoomToRentRadarUnitTests` and `RoomToRentRadarUIAppiumTests`. The QA subagent **MUST NEVER modify production code in any production project** (`RoomToRentRadar.Domain`, `RoomToRentRadar.Ports`, `RoomToRentRadar.Application`, `RoomToRentRadar.Datastore.Sqlite`, `RoomToRentRadar.Datastore.CosmosDb`, `RoomToRentRadar.Infrastructure`, `RoomToRentRadar.Api`, `RoomToRentRadar.Client.Adapters`, or `RoomToRentRadar`). If a test fails (Red), QA must output a defect report to the Dev roles.

### 1.3. Clean / Hexagonal Architectural Boundaries
- **Core Layer (`RoomToRentRadar.Domain/`, `RoomToRentRadar.Ports/`, `RoomToRentRadar.Application/`):**
  - **FORBIDDEN:** `using Microsoft.Maui`, `using Microsoft.Maui.Controls`.
  - **FORBIDDEN:** `using Microsoft.Data.Sqlite`, `using Microsoft.Azure.Cosmos`.
  - **FORBIDDEN:** `using System.Net.Http`, `using Microsoft.AspNetCore.*`, `using Microsoft.IdentityModel.*`.
  - **Implicit / Global Usings Isolation:** Even with `<ImplicitUsings>enable</ImplicitUsings>`, classes in `RoomToRentRadar.Domain/`, `RoomToRentRadar.Ports/`, and `RoomToRentRadar.Application/` **MUST NOT** reference any types from `Microsoft.Maui.*`, `System.Net.Http.*`, `Microsoft.AspNetCore.*`, `Microsoft.IdentityModel.*`, `RoomToRentRadar.Pages`, `RoomToRentRadar.PageModels`, or `RoomToRentRadar.Data`.
  - Core MUST remain Pure C# (POCOs), containing only Business Rules, Entities, Value Objects, Use Cases, and Ports (Interfaces).
- **Presentation Layer (UI / PageModels):**
  - **FORBIDDEN:** Direct SQL queries, SQLite database connections, or direct `HttpClient` calls in XAML code-behind (`*.xaml.cs`) or `PageModel`.
  - All data retrieval and command execution must pass through client gateway interfaces (`I*Gateway` in `RoomToRentRadar.Client.Adapters`) that call the REST API (`docs/adr/adr-003`). The MAUI project MUST NOT reference `RoomToRentRadar.Application`, `RoomToRentRadar.Infrastructure` or `RoomToRentRadar.Datastore.*`, and MUST NOT contain secrets (JWT keys, API keys) or force mocks on.

### 1.4. Modern .NET MAUI Standards (from davidortinau/maui-skills)
- **Zero Obsolete APIs:** Deprecated Xamarin.Forms APIs (e.g., `MessagingCenter`, `RelativeLayout`) are strictly banned in .NET 10.
- **Compiled Bindings:** Every `DataTemplate` in a `CollectionView` / `ListView` MUST specify `x:DataType`.
- **Android Emulator Networking:** API calls from the Android Emulator to localhost services MUST use IP `10.0.2.2`, never `localhost`.

### 1.5. Universal Production-Grade Engineering Principles (Across All Features)
Every feature implementation (Auth, Listings, Search, Maps, Media, Booking, Notifications) MUST strictly enforce the following 6 core architectural principles:

#### 1.5.1. Strict Input Boundaries & Explicit Whitelisting (No Mass Assignment)
- **Zero Trust on Request DTOs:** Request DTOs from clients MUST ONLY contain fields that the client is explicitly permitted to supply. NEVER include server-controlled state flags, security status, audit timestamps, or role claims (e.g., `IsVerified`, `Status`, `CreatedAt`, `CreatedBy`, `ApprovedAt`, `Role`) in public request contracts.
- **Whitelist Validation:** For any string representing an enum or discrete set of states (e.g., room types, sort orders, user roles), validation MUST use explicit whitelisting via `Enum.IsDefined` and explicit allowed value sets. Blacklisting (checking only for disallowed values) is STRICTLY PROHIBITED.

#### 1.5.2. Concurrency Control & Idempotency by Design
- **Mandatory Optimistic Locking on State Mutations:** Any Aggregate or entity whose state evolves over time (e.g., Listings, Refresh Tokens, Booking/Deposits, User Profiles) MUST maintain a concurrency token (`Version INTEGER` column on SQLite, exposed as `ConcurrencyToken` in the domain and as `ETag`/`If-Match` in the API — `docs/erd.md` §1, `docs/adr/adr-012`).
- **Conflict Handling:** Every `UpdateAsync` operation MUST enforce concurrency conditions. Upon detecting a version mismatch, the repository MUST throw `ConcurrencyConflictException` mapped to HTTP `409 Conflict`.
- **Idempotency for Side-Effects:** Endpoints and operations that perform financial, booking, or notification actions MUST support idempotent retries to prevent duplicate processing (e.g., double booking, duplicate push alerts).

#### 1.5.3. Defensive Domain Design & State Machine Integrity (Make Invalid States Unrepresentable)
- **Invariant Self-Protection:** Entity constructors and factory methods MUST act as guards, throwing `DomainValidationException` immediately when encountering invalid invariants (e.g., Price <= 0, Area <= 0, EndDate < StartDate, empty/whitespace IDs or hashes).
- **Explicit State Transitions:** Direct mutation of status fields is FORBIDDEN. Status transitions MUST occur through explicit domain methods (e.g., `listing.SubmitForReview()`, `listing.Approve()`, `listing.MarkAsRented()`) that validate prerequisite states.

#### 1.5.4. Secure-by-Default & Fail-Fast Configuration
- **Mock Adapters Disabled by Default:** All mock implementations (Mock SSO, Mock Geolocation, Mock Storage, Mock Payment, Mock Push) MUST default to disabled (`false`). Mocks are STRICTLY FORBIDDEN from being enabled in root `appsettings.json` and are only permitted in `appsettings.Development.json` or automated test fixtures.
- **Fail-Fast on Missing Secrets/Dependencies:** In Non-Development environments, if critical configurations (signing keys, database connection strings, cloud storage credentials, third-party API keys) are missing, the application MUST throw `InvalidOperationException` and terminate startup immediately. Generating ephemeral in-memory keys or silently falling back to mock services in production is STRICTLY PROHIBITED.

#### 1.5.5. Realistic Adversarial Testing & Async Hygiene
- **No Illusion Tests:** Test suites MUST NOT use dummy strings or bypass real validation to achieve a green test pass. Tests MUST supply cryptographically valid tokens, real binary headers for file uploads, and valid coordinates.
- **Mandatory Negative Testing:** In addition to Happy Path tests, every feature MUST include negative test suites: malformed payloads, unauthorized roles, expired/corrupted tokens, boundary values, and concurrent execution collisions.
- **Async Test Purity:** All test methods exercising asynchronous operations (`Task`, `ValueTask`) MUST be declared `public async Task` and await results. Exception assertions on async methods MUST use `Assert.ThrowsAsync<TException>(async () => await ...)`.

#### 1.5.6. Data Scoping & Least Privilege Information Projection (Anti-IDOR & Privacy)
- **Ownership Verification (Anti-IDOR):** Read, update, and delete operations on user-owned resources (Favourites, Private Bookings, User Reports, Draft Listings) MUST scope queries by the authenticated user's ID (`WHERE Id = @id AND OwnerId = @currentUserId`).
- **Least Privilege DTO Projection:** Domain entities and database documents MUST NEVER be returned directly from API controllers. Responses MUST always be projected through dedicated Response DTOs that omit sensitive information (national ID numbers, bank details, internal hashes, unmasked phone numbers).

### 1.6. SOLID Architectural Principles for Hexagonal DDD
Every component, interface, and class implementation across all layers MUST strictly adhere to the 5 SOLID principles:

#### 1.6.1. Single Responsibility Principle (SRP)
- **Use Case Segregation:** Every Use Case class (`*UseCase`) in `RoomToRentRadar.Application` MUST serve exactly one business capability and expose exactly one public execution method: `Task<TResult> ExecuteAsync(TRequest request, CancellationToken ct)`. Creating multipurpose "Service" classes (e.g., `RoomService`, `UserService` containing multiple arbitrary business operations) is STRICTLY PROHIBITED.
- **Domain Invariant Purity:** Aggregates and Entities in `RoomToRentRadar.Domain` MUST solely own business rules, domain state transitions, and internal invariant validations. They MUST NEVER contain DTO mapping, persistence logic, UI formatting, or external communication.
- **DTO Separation:** Request DTOs are strictly input carriers; Response DTOs are strictly output projections. Never reuse the same DTO for both inbound mutations and outbound projections.

#### 1.6.2. Open / Closed Principle (OCP)
- **Extensibility via Abstractions:** The system is open for extension but closed for modification. Introducing new business rules, sorting/filtering criteria, or external integrations MUST be achieved by implementing existing abstractions or adding new Ports/Adapters, NOT by mutating stable domain models with growing `if-else` or `switch-case` branches.
- **Specification & Strategy Patterns:** Complex business queries (e.g., room discovery filters, pricing algorithms) MUST be composed using Domain Specifications (`ISpecification<T>`) or Strategies rather than embedding monolithic conditional logic in Use Cases.
- **Pluggable Adapters:** Adding new storage providers (e.g., PostgreSQL), media engines (Cloudinary), or notification channels (Firebase/SMS) must be achieved entirely within `Infrastructure`/`Datastore` via new `I*Port`/`I*Repository` implementations without touching a single line of `Domain` or `Application` code.

#### 1.6.3. Liskov Substitution Principle (LSP)
- **Behavioral Equivalence of Adapters:** Any implementation of an Outbound Port (`I*Repository`, `I*Port`) MUST be fully substitutable for any other implementation without altering the correctness or behavior of the consuming Use Case.
- **Prohibited Pseudo-Implementations:** No repository or adapter implementation is permitted to throw `NotImplementedException`, `NotSupportedException`, or silently swallow operations for methods defined on its Port interface.
- **Consistent Contract Semantics:** 
  - If a resource is missing, all repository implementations of `GetByIdAsync` MUST return `null` (or throw `EntityNotFoundException` where explicitly mandated by the Port contract). One implementation returning `null` while another throws an unhandled database exception is a direct violation of LSP.
  - Concurrency token checks (`ConcurrencyConflictException`) must be enforced identically across SQLite and In-Memory implementations.

#### 1.6.4. Interface Segregation Principle (ISP)
- **Single-Action Inbound Ports:** Every driving interface in `RoomToRentRadar.Ports/Inbound/UseCases/` MUST contain exactly 1 method (`ExecuteAsync`). API controllers must depend only on the specific UseCase interface they invoke; MAUI PageModels depend only on the specific client gateway (`I*Gateway`) they need.
- **Cohesive Driven Ports:** Driven interfaces in `RoomToRentRadar.Ports/Outbound/` MUST be cohesive and finely targeted. Avoid bloated "God Interfaces" (e.g., an `IRoomRepository` with 30 methods covering search, statistics, media, and moderation). Where appropriate, segregate read operations from write operations or separate specialized queries into dedicated query ports.

#### 1.6.5. Dependency Inversion Principle (DIP)
- **Dependency Flow Toward Abstractions:** High-level modules (`Domain`, `Ports`, `Application`) MUST NOT depend on low-level modules (`Datastore`, `Infrastructure`, `Api`, `RoomToRentRadar` MAUI). Both high-level and low-level modules depend exclusively on abstractions defined in `RoomToRentRadar.Ports`.
- **Pure Constructor Injection:** UseCases and Adapters MUST receive all external dependencies via constructor injection typed strictly as Interfaces (Ports). Instantiating concrete infrastructure services or datastore contexts directly via `new` within `Application` or `Domain` is STRICTLY PROHIBITED.
- **No Inverted Framework References:** Core assemblies MUST NOT directly reference SDKs or frameworks (e.g., EF Core, Cosmos SDK, SQLite, ASP.NET Core Http, MAUI controls). Frameworks adapt to Core, never Core to frameworks.

---

## 2. STOP-AND-ASK TRIGGERS (WHEN TO PAUSE AND PROMPT)

Agents **MUST NOT GUESS**. Agents MUST pause execution and ask the user in the following four scenarios:

| Trigger Scenario | Required Agent Prompt | Purpose / Rationale |
| :--- | :--- | :--- |
| **1. Installing New NuGet Packages** | *"This feature requires NuGet package `X` (version `Y`). Do you approve adding it to the csproj?"* | Prevents bloated dependencies and compatibility conflicts with .NET 10 MAUI. |
| **2. Editing Sensitive Project Files** | *"I need to modify `<Project>.csproj`, `RoomToRentRadar.slnx`, or `AndroidManifest.xml` due to [Reason]. Do you authorize this change?"* | Protects TargetFrameworks, Keystores, and native permissions across all solution projects from breaking. |
| **3. Modifying Shared Contracts / DTOs** | *"I need to alter property `Z` in shared DTO `RoomDetailDto`. This affects both MAUI and Web API. Do you approve?"* | Prevents contract drift and breaking communication between Client and Server. |
| **4. Missing API Keys or Parameters** | *"The Google Maps feature requires an API key. Would you like to use a Mock Map Service or provide a valid key?"* | Prevents the agent from inventing dummy keys that crash during real execution. |

---

## 3. POST-FLIGHT VERIFICATION CHECKLIST
Before completing any turn, the agent must verify:
- [ ] Code compiles cleanly with 0 errors across both Client and Web API (`dotnet build RoomToRentRadar/RoomToRentRadar.csproj` and `dotnet build RoomToRentRadar.Api/RoomToRentRadar.Api.csproj`)?
- [ ] All relevant unit tests compile and execute (`dotnet test RoomToRentRadarUnitTests/RoomToRentRadarUnitTests.csproj`)?
- [ ] The Core layer (`RoomToRentRadar.Domain/`, `RoomToRentRadar.Ports/`, `RoomToRentRadar.Application/`) has zero explicit or implicit references to UI (`Microsoft.Maui.*`), HTTP/Web (`System.Net.Http.*`, `Microsoft.AspNetCore.*`), Identity/JWT (`Microsoft.IdentityModel.*`), or Database libraries?
- [ ] Any newly created Use Case (`I*UseCase` -> `*UseCase`), Repository (`I*Repository` -> `*Repository`), or Adapter (`I*Port` -> `*Adapter`) is properly registered in `RoomToRentRadar.Application/DependencyInjection.cs`, `RoomToRentRadar.Datastore.Sqlite/DependencyInjection.cs`, `RoomToRentRadar.Infrastructure/DependencyInjection.cs`, `RoomToRentRadar.Client.Adapters/DependencyInjection.cs`, `MauiProgram.cs`, or `RoomToRentRadar.Api/Program.cs`?
- [ ] All new `CollectionView` / `ListView` elements and root `ContentPage` files declare `x:DataType`?
- [ ] No server-managed fields (`Status`, `IsVerified`, `Role`, `CreatedAt`) exposed in public Request DTOs (Zero Trust & Whitelisting respected)?
- [ ] Mutations on evolving state implement optimistic concurrency (ETag / version check) and throw `ConcurrencyConflictException` on collision?
- [ ] Domain entities and value objects enforce invariants in constructors, rejecting invalid states with `DomainValidationException`?
- [ ] Mock services default to `false` in base configuration and fail fast outside `Development` if external secrets/dependencies are missing?
- [ ] Unit tests for async methods use `public async Task` and `Assert.ThrowsAsync<T>()` without sync wrappers or fake string shortcuts?
- [ ] Read/update operations on user-owned resources enforce ownership checks against the authenticated `sub` ID (Anti-IDOR)?
- [ ] SOLID Principles strictly respected: Single-action Inbound Ports with 1 `ExecuteAsync` (SRP/ISP), extensible via Specifications/Ports without modifying stable core (OCP), Port implementations maintain identical contract and exception behavior (LSP), and Core depends strictly on Port abstractions (DIP)?
- [ ] No files outside the assigned task scope were modified (Two-File Rule respected)?
