# AGENT OPERATIONAL GUARDRAILS (PROJECT SAFETY POLICY)
> **Project:** RoomToRentRadar — Group 2 (ICTU)  
> **Platform:** .NET MAUI 10 (Client) + ASP.NET Core (REST API) + Azure Cosmos DB (Spatial NoSQL)

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
- **QA Subagent Sandbox:** The QA subagent is only permitted to create and modify test files within `RoomToRentRadarUnitTests` and `RoomToRentRadarUIAppiumTests`. The QA subagent **MUST NEVER modify production code in any of the 7 production projects** (`RoomToRentRadar.Domain`, `RoomToRentRadar.Ports`, `RoomToRentRadar.Application`, `RoomToRentRadar.Datastore`, `RoomToRentRadar.Infrastructure`, `RoomToRentRadar.Api`, or `RoomToRentRadar`). If a test fails (Red), QA must output a defect report to the Dev roles.

### 1.3. Clean / Hexagonal Architectural Boundaries
- **Core Layer (`RoomToRentRadar.Domain/`, `RoomToRentRadar.Ports/`, `RoomToRentRadar.Application/`):**
  - **FORBIDDEN:** `using Microsoft.Maui`, `using Microsoft.Maui.Controls`.
  - **FORBIDDEN:** `using Microsoft.Data.Sqlite`, `using Microsoft.Azure.Cosmos`.
  - **FORBIDDEN:** `using System.Net.Http`, `using Microsoft.AspNetCore.*`, `using Microsoft.IdentityModel.*`.
  - **Implicit / Global Usings Isolation:** Even with `<ImplicitUsings>enable</ImplicitUsings>`, classes in `RoomToRentRadar.Domain/`, `RoomToRentRadar.Ports/`, and `RoomToRentRadar.Application/` **MUST NOT** reference any types from `Microsoft.Maui.*`, `System.Net.Http.*`, `Microsoft.AspNetCore.*`, `Microsoft.IdentityModel.*`, `RoomToRentRadar.Pages`, `RoomToRentRadar.PageModels`, or `RoomToRentRadar.Data`.
  - Core MUST remain Pure C# (POCOs), containing only Business Rules, Entities, Value Objects, Use Cases, and Ports (Interfaces).
- **Presentation Layer (UI / PageModels):**
  - **FORBIDDEN:** Direct SQL queries, SQLite database connections, or direct `HttpClient` calls in XAML code-behind (`*.xaml.cs`) or `PageModel`.
  - All data retrieval and command execution must pass through Use Case Input Ports.

### 1.4. Modern .NET MAUI Standards (from davidortinau/maui-skills)
- **Zero Obsolete APIs:** Deprecated Xamarin.Forms APIs (e.g., `MessagingCenter`, `RelativeLayout`) are strictly banned in .NET 10.
- **Compiled Bindings:** Every `DataTemplate` in a `CollectionView` / `ListView` MUST specify `x:DataType`.
- **Android Emulator Networking:** API calls from the Android Emulator to localhost services MUST use IP `10.0.2.2`, never `localhost`.

---

## 2. STOP-AND-ASK TRIGGERS (WHEN TO PAUSE AND PROMPT)

Agents **MUST NOT GUESS**. Agents MUST pause execution and ask the user in the following four scenarios:

| Trigger Scenario | Required Agent Prompt | Purpose / Rationale |
| :--- | :--- | :--- |
| **1. Installing New NuGet Packages** | *"This feature requires NuGet package `X` (version `Y`). Do you approve adding it to the csproj?"* | Prevents bloated dependencies and compatibility conflicts with .NET 10 MAUI. |
| **2. Editing Sensitive Project Files** | *"I need to modify `<Project>.csproj`, `RoomToRentRadar.slnx`, or `AndroidManifest.xml` due to [Reason]. Do you authorize this change?"* | Protects TargetFrameworks, Keystores, and native permissions across all 9 projects from breaking. |
| **3. Modifying Shared Contracts / DTOs** | *"I need to alter property `Z` in shared DTO `RoomDetailDto`. This affects both MAUI and Web API. Do you approve?"* | Prevents contract drift and breaking communication between Client and Server. |
| **4. Missing API Keys or Parameters** | *"The Google Maps feature requires an API key. Would you like to use a Mock Map Service or provide a valid key?"* | Prevents the agent from inventing dummy keys that crash during real execution. |

---

## 3. POST-FLIGHT VERIFICATION CHECKLIST
Before completing any turn, the agent must verify:
- [ ] Code compiles cleanly with 0 errors across both Client and Web API (`dotnet build RoomToRentRadar/RoomToRentRadar.csproj` and `dotnet build RoomToRentRadar.Api/RoomToRentRadar.Api.csproj`)?
- [ ] All relevant unit tests compile and execute (`dotnet test RoomToRentRadarUnitTests/RoomToRentRadarUnitTests.csproj`)?
- [ ] The Core layer (`RoomToRentRadar.Domain/`, `RoomToRentRadar.Ports/`, `RoomToRentRadar.Application/`) has zero explicit or implicit references to UI (`Microsoft.Maui.*`), HTTP/Web (`System.Net.Http.*`, `Microsoft.AspNetCore.*`), Identity/JWT (`Microsoft.IdentityModel.*`), or Database libraries?
- [ ] Any newly created Use Case (`I*UseCase` -> `*UseCase`), Repository (`I*Repository` -> `*Repository`), or Adapter (`I*Port` -> `*Adapter`) is properly registered in `RoomToRentRadar.Application/DependencyInjection.cs`, `RoomToRentRadar.Datastore/DependencyInjection.cs`, `RoomToRentRadar.Infrastructure/DependencyInjection.cs`, `MauiProgram.cs`, or `RoomToRentRadar.Api/Program.cs`?
- [ ] All new `CollectionView` / `ListView` elements and root `ContentPage` files declare `x:DataType`?
- [ ] No files outside the assigned task scope were modified (Two-File Rule respected)?
