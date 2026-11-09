---
name: dependency-rule-guard
description: Enforces the Clean / Hexagonal Architecture dependency rule, keeping Core free from UI and DB dependencies.
---

# Dependency Rule & Architecture Boundary Guard

Ensures architectural integrity by keeping business domain logic completely decoupled from volatile external frameworks (.NET MAUI, SQLite, Azure Cosmos DB).

## 1. The Inviolable Dependency Rule

```text
[ Presentation (MAUI) ] ──┐
                          ▼
[ Infrastructure (DB) ] ───► [ Core (Domain & Application) ]
```

- All source code dependencies **MUST POINT INWARD** toward the Core.
- The **Core** layer knows nothing about the **Presentation** (UI) or **Infrastructure** (Database/Network) layers.

## 2. Red Line Violation Checklist
If any file in the Core layer violates any of the following checks, the build is considered **architecturally broken**:
- [ ] Contains `using Microsoft.Maui` or `using Microsoft.Maui.Controls` in Core -> **VIOLATION**.
- [ ] Contains `using Microsoft.Data.Sqlite` or `using Microsoft.Azure.Cosmos` in Core -> **VIOLATION**.
- [ ] Contains `using System.Net.Http` in Core -> **VIOLATION**.
- [ ] A PageModel invokes SQL commands or manages database connections directly without an Input Port -> **VIOLATION**.

## 3. Remediation Pattern (Inversion of Control — Example)
When the Core requires an external capability (e.g., fetching listings from a remote API, accessing GPS coordinates):
1. The Core defines an **Interface (Output Port following `I*Port` convention)**:
   ```csharp
   // Example Output Port — actual port names are defined per User Story
   public interface IMapRadarPort
   {
       Task<(double Latitude, double Longitude)> GetCurrentLocationAsync(CancellationToken ct = default);
   }
   ```
2. The Infrastructure layer implements this interface using MAUI device APIs (`*Adapter` convention):
   ```csharp
   public class MauiMapRadarAdapter : IMapRadarPort
   {
       public async Task<(double Latitude, double Longitude)> GetCurrentLocationAsync(...)
       {
           var location = await Geolocation.Default.GetLocationAsync();
           return (location.Latitude, location.Longitude);
       }
   }
   ```
3. Register the binding in `MauiProgram.cs`:
   `builder.Services.AddSingleton<IMapRadarPort, MauiMapRadarAdapter>();`
