# ADR-003: Client ↔ server communication — REST/JSON via typed client gateways; the client does not host the Application layer

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** Group 2 (proposed by Claude, approved by Lại Văn Thịnh)
- **Related:** ADR-001, ADR-004, ADR-009, ADR-012; [system-architecture.md §5](../system-architecture.md#5-mobile-client-architecture)

## Context
Today `MauiProgram.cs` calls `AddApplicationServices()` + `AddInfrastructure()` + `AddDatastore()`. That means the **server use cases run inside the phone** against in-memory/SQLite repositories and never talk to the API; resolving any Auth use case would even crash because `IJwtTokenServicePort` is not registered without JWT settings. Data must be shared between users (listings, favourites sync, chat), so the phone must call the API. We also want the fastest perceived performance and good offline UX, while keeping PageModels free of `HttpClient` (GUARDRAILS 1.3).

## Options considered
| Option | Pros | Cons |
|---|---|---|
| A. Client reuses server Inbound Ports (`ISearchRoomsUseCase`) with HTTP implementations | One set of interfaces | Server signatures carry server concerns (e.g. `userId` parameters taken from the token); confusing to have two implementations of the same use case with different semantics |
| **B. Client gateways** (`IRoomsGateway` …) in a client adapters library, reusing the shared **DTOs** from `RoomToRentRadar.Ports/Contracts` | Clear separation; signatures fit the client (no userId, cancellation, cache policy); unit-testable on `net10.0`; decorators for caching | One more project; interfaces per feature |
| C. Generated client (NSwag/Kiota) from OpenAPI | No hand-written HTTP code | Generator tooling + regeneration step; generated DTOs duplicate `Ports/Contracts` |
| Transport: gRPC / GraphQL | Compact / flexible | Poor fit for MAUI on all platforms (gRPC-Web), extra learning, caching harder; no clear win at this scale |

## Decision
1. **Transport:** HTTPS REST with JSON (`System.Text.Json` + **source-generated** `JsonSerializerContext` for speed and trimming safety), response compression (Brotli/Gzip) on the server, real-time chat events over SignalR (ADR-009).
2. **New project `RoomToRentRadar.Client.Adapters` (`net10.0` class library)** — references `RoomToRentRadar.Ports` only (for `Contracts`). Folders:
   - `Abstractions/` — `I<Feature>Gateway` interfaces (`IAuthGateway`, `IRoomsGateway`, `IFavouritesGateway`, `ISavedSearchesGateway`, `IListingsGateway`, `IModerationGateway`, `IChatGateway`, `INotificationsGateway`, `ICampusGateway`), `ISessionStore`, `ICacheStore`.
   - `Http/` — `Http<Feature>Gateway` implementations using a typed `HttpClient` from `IHttpClientFactory`; `AuthTokenHandler` (`DelegatingHandler`: attaches bearer, single-flight refresh on 401); resilience via `Microsoft.Extensions.Http.Resilience` (timeout 10 s; retry ×2 with jitter **only for GET**).
   - `Caching/` — `Cached<Feature>Gateway` decorators (ADR-004).
   - `Realtime/` — `SignalRChatConnection`.
3. **MAUI app** (`RoomToRentRadar`) references **only** `RoomToRentRadar.Ports` and `RoomToRentRadar.Client.Adapters`. It no longer references `Application`, `Infrastructure` or `Datastore.Sqlite`; `MauiProgram` registers gateways, the SQLite cache, PageModels and Pages. Device-specific implementations (`SecureStorage`, `Preferences`, connectivity, push token) live in `RoomToRentRadar/DeviceAdapters/` implementing interfaces from `Client.Adapters/Abstractions`.
4. **PageModels depend only on gateway interfaces** — never on `HttpClient`, SQLite or SignalR types.
5. Base URL from configuration: Android emulator `http://10.0.2.2:5000`, physical device/dev tunnel URL, release HTTPS URL.
6. Pagination, errors and concurrency follow ADR-012.

## Why this is the best fit (performance + UX)
- **Perceived speed:** gateways sit behind cache decorators, so screens render cached data in < 1 s and refresh silently (ADR-004) — a major UX gain on 4G.
- **Small payloads:** list and map endpoints return the lightweight `RoomCardDto` (~0.5 KB), details only on demand; JSON source generation avoids reflection cost on low-end Android.
- **Simplicity for a team new to APIs:** REST + JSON is debuggable in a browser/Scalar; no code generators; one `HttpClient` pattern repeated per feature.
- **Testability:** `Client.Adapters` targets `net10.0`, so it is unit-tested in the existing NUnit project (MAUI multi-target projects cannot be referenced by `net10.0` tests).

## Consequences
- Positive: fixes the runtime-crash risk in `MauiProgram`; PageModels testable with NSubstitute gateways.
- Negative: one new project + csproj/slnx edits (GUARDRAILS stop-and-ask → this ADR is the approval request; implement in a dedicated task).
- Follow-up: GUARDRAILS 1.3 wording "PageModels call Input Ports" becomes "PageModels call client gateways"; update `AGENTS.md`; new architecture test "MAUI project must not reference Application/Infrastructure/Datastore".

## Compliance
Architecture test on project references; review checklist: no `HttpClient`/`SqliteConnection` in `PageModels/` or `Pages/`.
